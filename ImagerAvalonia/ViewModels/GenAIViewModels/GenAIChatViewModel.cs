using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services.GenAI;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels.GenAIViewModels
{
    public partial class GenAIChatViewModel : ViewModelBase
    {
        private readonly IAnthropicChatService _chatService;
        private readonly EquipmentWorkspace _equipmentWorkspace;
        private readonly GlobalDefinedSettingsViewModel _globalSettings;
        private readonly ExperimentManager _experimentManager;

        private readonly JArray _conversation = new();

        [ObservableProperty] private ObservableCollection<ChatMessageViewModel> _messages = new();
        [ObservableProperty] private string _inputText = string.Empty;
        [ObservableProperty] private bool _isBusy;

        public GenAIChatViewModel(
            IAnthropicChatService chatService,
            EquipmentWorkspace equipmentWorkspace,
            GlobalDefinedSettingsViewModel globalSettings,
            ExperimentManager experimentManager)
        {
            _chatService = chatService;
            _equipmentWorkspace = equipmentWorkspace;
            _globalSettings = globalSettings;
            _experimentManager = experimentManager;

            Messages.Add(new ChatMessageViewModel(ChatRole.System,
                "Describe the experiment you want and I'll generate it from your current hardware and acquisition settings."));
        }

        [RelayCommand]
        private async Task SendAsync()
        {
            var userText = InputText?.Trim();
            if (string.IsNullOrEmpty(userText) || IsBusy)
                return;

            InputText = string.Empty;
            Messages.Add(new ChatMessageViewModel(ChatRole.User, userText));

            IsBusy = true;
            try
            {
                var contextualizedText = BuildContextualizedUserMessage(userText);
                _conversation.Add(new JObject
                {
                    ["role"] = "user",
                    ["content"] = contextualizedText
                });

                var result = await _chatService.SendMessageAsync(ImagGeneratorPrompt.SystemPrompt, _conversation);

                if (!result.Success)
                {
                    if (_conversation.Count > 0)
                        _conversation.RemoveAt(_conversation.Count - 1);

                    Messages.Add(new ChatMessageViewModel(ChatRole.System, result.ErrorMessage ?? "Unknown error.", isError: true));
                    return;
                }

                _conversation.Add(new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = result.AssistantText
                });

                await HandleAssistantResponseAsync(result.AssistantText!);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleAssistantResponseAsync(string assistantText)
        {
            JObject parsed;
            try
            {
                parsed = ParseJsonResponse(assistantText);
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessageViewModel(ChatRole.Assistant,
                    $"Could not parse the response as JSON ({ex.Message}). Raw response:\n{assistantText}", isError: true));
                return;
            }

            var status = parsed["status"]?.ToString();
            switch (status)
            {
                case "OK":
                    await HandleOkAsync(parsed);
                    break;

                case "INFORMATION_MISSING":
                {
                    var missing = parsed["missing"] as JArray;
                    var missingList = missing != null ? string.Join(", ", missing.Select(m => m.ToString())) : null;
                    var prompt = parsed["prompt"]?.ToString() ?? "More information is needed.";
                    var text = missingList != null ? $"{prompt}\n\nMissing: {missingList}" : prompt;
                    Messages.Add(new ChatMessageViewModel(ChatRole.Assistant, text));
                    break;
                }

                case "PYTHON_REQS_NOT_SATISFIED":
                {
                    var reason = parsed["reason"]?.ToString() ?? "This request needs a capability that isn't available.";
                    var prompt = parsed["prompt"]?.ToString();
                    var text = prompt != null ? $"{reason}\n\n{prompt}" : reason;
                    Messages.Add(new ChatMessageViewModel(ChatRole.Assistant, text, isError: true));
                    break;
                }

                case "CLARIFICATION_NEEDED":
                {
                    var ambiguity = parsed["ambiguity"]?.ToString();
                    var prompt = parsed["prompt"]?.ToString() ?? "Could you clarify your request?";
                    var text = ambiguity != null ? $"{ambiguity}\n\n{prompt}" : prompt;
                    Messages.Add(new ChatMessageViewModel(ChatRole.Assistant, text));
                    break;
                }

                default:
                    Messages.Add(new ChatMessageViewModel(ChatRole.Assistant,
                        $"Unrecognized response status '{status}'. Raw response:\n{assistantText}", isError: true));
                    break;
            }
        }

        private async Task HandleOkAsync(JObject parsed)
        {
            var imag = parsed["imag"] as JObject;
            if (imag is null)
            {
                Messages.Add(new ChatMessageViewModel(ChatRole.Assistant,
                    "The response had status OK but no 'imag' document was present.", isError: true));
                return;
            }

            try
            {
                var imagJson = imag.ToString(Formatting.None);
                await _experimentManager.ParseLoadedExperiment(imagJson);
                Messages.Add(new ChatMessageViewModel(ChatRole.Assistant, "Generated and loaded the experiment."));
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessageViewModel(ChatRole.Assistant,
                    $"The generated .imag could not be loaded: {ex.Message}", isError: true));
            }
        }

        private static JObject ParseJsonResponse(string text)
        {
            var trimmed = text.Trim();

            if (trimmed.StartsWith("```"))
            {
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline >= 0)
                    trimmed = trimmed[(firstNewline + 1)..];
                if (trimmed.EndsWith("```"))
                    trimmed = trimmed[..^3];
                trimmed = trimmed.Trim();
            }

            return JObject.Parse(trimmed);
        }

        private string BuildContextualizedUserMessage(string userText)
        {
            var currentEquipment = JObject.FromObject(_equipmentWorkspace,
                JsonSerializer.Create(FullEquipmentStateSerializer.Settings));

            var detections = _globalSettings.Acquisitions
                .ToDictionary(a => a.Name, a => a.DetectionSettings.Settings);
            var detectionsJson = JObject.FromObject(detections,
                JsonSerializer.Create(MeasurementSerializer.SettingsForStorage));

            return
                "Hardware settings (currentequipment):\n```json\n" + currentEquipment.ToString(Formatting.None) + "\n```\n\n" +
                "Acquisition settings (detections):\n```json\n" + detectionsJson.ToString(Formatting.None) + "\n```\n\n" +
                "User request: " + userText;
        }
    }
}
