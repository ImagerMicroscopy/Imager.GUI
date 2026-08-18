using Autofac;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using ImagerAvalonia;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Tests.ViewModels;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using ImagerAvalonia.Services.Storage;

namespace Imager.Tests.ViewModels
{

    public static class EquipmentMockResponses
    {
        public const string AvailableDetectorsRequest = "{\"action\":\"listavailabledetectors\"}";
        public const string AvailableDetectorsResponse =
            "{\"responsetype\":\"availabledetectors\",\"detectornames\":[\"DummyCam1\",\"DummyCam2\"]}";

        public const string DetectorPropertiesResponse = @"
        {
            ""responsetype"":""detectorproperties"",
            ""detectorproperties"":[
                {""descriptor"":""Exure time"",""kind"":""numeric"",""propertycode"":0,""value"":5.0e-3},
                {""availableoptions"":[""16"",""32"",""64"",""128"",""256"",""512"",""1024"",""1280"",""1536"",""2048""],
                    ""current"":""128"",""descriptor"":""Sensor cropping 1"",""kind"":""discrete"",""propertycode"":1},
                {""availableoptions"":[""16"",""32"",""64"",""128"",""256"",""512"",""1024"",""1280"",""1536"",""2048""],
                    ""current"":""128"",""descriptor"":""Sensor cropping 2"",""kind"":""discrete"",""propertycode"":2},
                {""availableoptions"":[""1"",""2"",""4""],
                    ""current"":""1"",""descriptor"":""Binning"",""kind"":""discrete"",""propertycode"":3}
            ],
            ""framerate"":200.0
        }";

        public const string AvailableEquipmentResponse = @"
        {
            ""responsetype"":""availableequipment"",
            ""equipment"":[
                {
                    ""availablelightsources"":[],
                    ""availablemovablecomponents"":[
                        {""componentname"":""fw"",""possiblesettings"":[""DAPI"",""GFP"",""YFP"",""RFP"",""640""],""type"":""discretemovablecomponent""},
                        {""componentname"":""sl"",""increment"":1,""maxvalue"":100,""minvalue"":0,""type"":""continuousmovablecomponent""}
                    ],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":false,
                    ""motorizedstageName"":"""",
                    ""name"":""fw1""
                },
                {
                    ""availablelightsources"":[],
                    ""availablemovablecomponents"":[
                        {""componentname"":""fw"",""possiblesettings"":[""DAPI/GFP/RFP/640"",""CFP/YFP/RFP""],""type"":""discretemovablecomponent""},
                        {""componentname"":""sl"",""increment"":1,""maxvalue"":100,""minvalue"":0,""type"":""continuousmovablecomponent""}
                    ],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":false,
                    ""motorizedstageName"":"""",
                    ""name"":""fw2""
                },
                {
                    ""availablelightsources"":[
                        {""allowmultiplechannels"":true,""cancontrolpower"":true,""channels"":[""ch1"",""ch2""],""name"":""ls""}
                    ],
                    ""availablemovablecomponents"":[],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":false,
                    ""motorizedstageName"":"""",
                    ""name"":""hello""
                },
                {
                    ""availablelightsources"":[
                        {""allowmultiplechannels"":true,""cancontrolpower"":true,""channels"":[""ch1"",""ch2""],""name"":""ls""}
                    ],
                    ""availablemovablecomponents"":[],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":false,
                    ""motorizedstageName"":"""",
                    ""name"":""hello2""
                },
                {
                    ""availablelightsources"":[],
                    ""availablemovablecomponents"":[],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":true,
                    ""motorizedstageName"":""dStage"",
                    ""name"":""Dummy stage""
                },
                {
                    ""availablelightsources"":[],
                    ""availablemovablecomponents"":[],
                    ""availablerobots"":[],
                    ""hasmotorizedstage"":false,
                    ""motorizedstageName"":"""",
                    ""name"":""SCCamera""
                }
            ]
        }";
    }




    public class MainViewModelTests
    {
        private void SetupEquipmentMocks(Mock<ComUtils> messagesMock)
        {
            messagesMock
                .Setup(m => m.SendDataRequest(
                    It.Is<string>(s => s == EquipmentMockResponses.AvailableDetectorsRequest),
                    It.Is<string>(s => s.Contains("\"responsetype\":\"availabledetectors\"")),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<byte[]>>()))
                .Callback<string, string, Action<string>, Action<byte[]>>((_, __, onResponse, ___) =>
                {
                    onResponse(EquipmentMockResponses.AvailableDetectorsResponse);
                });

            messagesMock
                .Setup(m => m.SendDataRequest(
                    It.Is<string>(s => s.Contains("getdetectorproperties")),
                    It.Is<string>(s => s.Contains("\"responsetype\":\"detectorproperties\"")),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<byte[]>>()))
                .Callback<string, string, Action<string>, Action<byte[]>>((_, __, onResponse, ___) =>
                {
                    onResponse(EquipmentMockResponses.DetectorPropertiesResponse);
                });

            messagesMock
                .Setup(m => m.SendDataRequest(
                    It.Is<string>(s => s.Contains("listavailableequipment")),
                    It.IsAny<string>(),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<byte[]>>()))
                .Callback<string, string, Action<string>, Action<byte[]>>((_, __, onResponse, ___) =>
                {
                    onResponse(EquipmentMockResponses.AvailableEquipmentResponse);
                });
        }







        private MainViewModel CreateViewModel(
            out Mock<ComUtils> messages,
            out Mock<IImagerConnectionHandler> connectionHandler,
            out Mock<IStageControl> stageControl,
            out ImageControlPanelViewModel imagePanel,
            out GlobalDefinedSettingsViewModel userDefinedAcquisitions,
            out Mock<SmartProcessingRegisterViewModel> processViewModel,
            out Mock<AcquisitionStateService> acquisitionState,
            out Mock<EquipmentState> equipmentState)
        {


            stageControl = new Mock<IStageControl>();
            connectionHandler = new Mock<IImagerConnectionHandler>();
            var stageControlVM = new Mock<StageControlPanelViewModel>(stageControl.Object);
            messages = new Mock<ComUtils>();
            userDefinedAcquisitions =  new GlobalDefinedSettingsViewModel();
            processViewModel = new Mock<SmartProcessingRegisterViewModel>();
            equipmentState = new Mock<EquipmentState>();
            acquisitionState = new Mock<AcquisitionStateService>(stageControl.Object, processViewModel.Object, messages.Object, equipmentState.Object);


            var imageRegionDisplayViewModel = new ImageRegionDisplayViewModel();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            var loggerMock = new Mock<ILogger>();
            loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

            var liveViewMock = new Mock<ImageDisplayViewModel>();
            var fieldViewMock = new Mock<FieldViewerViewModel>();
            var imageVmFactoryMock = new Mock<IImageDisplayViewModelFactory>();

            var workspaceData = new DataWorkspace();
            var acquisitionEngine = new AcquisitionEngine(ImagerCommunicationManager.Instance);
            var workspace = new ImagerWorkspace(
                new ExperimentBuilder(userDefinedAcquisitions, stageControl.Object, new Mock<ImagerAvalonia.Services.INodeFactory>().Object),
                acquisitionEngine,
                workspaceData,
                new Mock<Autofac.ILifetimeScope>().Object,
                loggerFactoryMock.Object,
                connectionHandler.Object,
                ImagerCommunicationManager.Instance,
                acquisitionState.Object
            );

            imagePanel = new ImageControlPanelViewModel(
                loggerFactoryMock.Object,
                liveViewMock.Object,
                fieldViewMock.Object,
                connectionHandler.Object,
                imageVmFactoryMock.Object,
                acquisitionState.Object,
                workspace
            );

            var builder = new ContainerBuilder();

            var stageMock = new Mock<IStageControl>();
            var comUtilsMock = new Mock<ComUtils>();
            var processVmMock = new Mock<SmartProcessingRegisterViewModel>();


            builder.RegisterInstance(imageRegionDisplayViewModel).As<ImageRegionDisplayViewModel>();
            builder.RegisterInstance(stageMock.Object).As<IStageControl>();
            builder.RegisterInstance(comUtilsMock.Object).As<ComUtils>();
            builder.RegisterInstance(processVmMock.Object).As<SmartProcessingRegisterViewModel>();
            builder.RegisterInstance(acquisitionState.Object).SingleInstance().As<AcquisitionStateService>();
            builder.RegisterInstance(stageControlVM.Object).SingleInstance();
            var statusVM = new Mock<StatusWindowViewModel>();
            var storageMock = new Mock<IStorageProvider>();
            var serializerMock = new Mock<IExperimentSerialization>();
            builder.RegisterInstance(statusVM.Object).As<StatusWindowViewModel>();
            builder.RegisterInstance(storageMock.Object).As<IStorageProvider>();
            builder.RegisterInstance(serializerMock.Object).As<IExperimentSerialization>();

            var container = builder.Build();

            App.SetTestContainer(container);


            var vm = new MainViewModel(
               stageControl.Object,
               imagePanel,
               userDefinedAcquisitions,
               processViewModel.Object,
               acquisitionState.Object,
               equipmentState.Object);

            SetupEquipmentMocks(messages);

            vm.InitializeEquipment();

            return new MainViewModel(
                stageControl.Object,
                imagePanel,
                userDefinedAcquisitions,
                processViewModel.Object,
                acquisitionState.Object,
                equipmentState.Object
                
            );
        }


        [Fact]
        public void Get_Parameters()
        {
            // Tests the getting of the parameters and their assignment
            var vm = CreateViewModel(out var messagesMock, out _, out _, out _, out _, out _, out _, out _);

            var acq_service = App.Container.Resolve<AcquisitionStateService>();

            Assert.NotNull(acq_service.SelectedAcquisition);
            var expectedNames = new List<string> { "DummyCam1", "DummyCam2" };
            foreach (var detector in acq_service.SelectedAcquisition.Detector)
            {
                Assert.Contains(detector.Name, expectedNames);
                Assert.True(detector.IsEnabled);
            }
            return;
        }



        [Fact]
        public void Add_and_RemoveAcquisition()
        {
            // Tests if single acquisition can be removed (at least one acquisition must remain)
            var vm = CreateViewModel(out var messagesMock, out _, out _, out _, out _, out _, out _, out _);

            var acq_service = App.Container.Resolve<AcquisitionStateService>();

            vm.CopyAcquisition();
            vm.RemoveAcquisition();

            Assert.NotNull(acq_service.SelectedAcquisition);
            Assert.Single(vm.SystemDefinedSettings.Acquisitions);
        }

        [Fact]
        public void Add_Remove_Experiment()
        {

            // Tests if an experiment can be added (at least one experiment must remain)
            var vm = CreateViewModel(out var messagesMock, out _, out _, out _, out _, out _, out _, out _);

            vm.AddExperiment();
            Assert.Single(vm.Experiments);
            vm.RemoveExperiment();
            Assert.Single(vm.Experiments);
        }

        [AvaloniaFact]
        public void Select_Experiment()
        {
            // Tests if selection changes properly when user selects an experiment
            var vm = CreateViewModel(out var messagesMock, out _, out _, out _, out _, out _, out _, out _);


            var mockVM = new Mock<MainViewModel>(MockBehavior.Strict, null!); // if ctor needs deps
            var view = new MainView();
            vm.AddExperiment();
            var control = view.FindControl<ListBox>("AvailableExperiments");
            Assert.NotNull(control);
            control.SelectedItem = vm.Experiments[0];

            Assert.NotNull(vm.SelectedExperiment);


        }
    }
}
