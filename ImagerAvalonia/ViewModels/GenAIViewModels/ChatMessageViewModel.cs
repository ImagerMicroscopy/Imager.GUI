using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagerAvalonia.ViewModels.GenAIViewModels
{
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    public partial class ChatMessageViewModel : ObservableObject
    {
        [ObservableProperty] private ChatRole _role;
        [ObservableProperty] private string _text = string.Empty;
        [ObservableProperty] private bool _isError;

        public bool IsUser => Role == ChatRole.User;
        public bool IsAssistant => Role == ChatRole.Assistant;
        public bool IsSystem => Role == ChatRole.System;

        public ChatMessageViewModel(ChatRole role, string text, bool isError = false)
        {
            Role = role;
            Text = text;
            IsError = isError;
        }
    }
}
