using ImagerAvalonia.Utils;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Tests.ViewModels
{
    internal class EquipmentMessages
    {





        public void SetupEquipmentMessageSimpleAcquistion(Mock<ComUtils> messagesMock)
        {
            messagesMock
                .Setup(m => m.SendDataRequest(
                    It.Is<string>(s => s == "{\"action\":\"listavailabledetectors\"}"),
                    It.Is<string>(s => s.Contains("\"responsetype\":\"availabledetectors\"")),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<byte[]>>()))
                .Callback<string, string, Action<string>, Action<byte[]>>((_, __, onResponse, ___) =>
                {
                    onResponse("{\"responsetype\":\"availabledetectors\",\"detectornames\":[\"DummyCam1\",\"DummyCam2\"]}");
                });

            messagesMock
                .Setup(m => m.SendDataRequest(
                    It.Is<string>(s => s.Contains("getdetectorproperties")),
                    It.Is<string>(s => s.Contains("\"responsetype\":\"detectorproperties\"")),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<byte[]>>()))
                .Callback<string, string, Action<string>, Action<byte[]>>((msg, _, onResponse, ___) =>
                {
                    var detectorName = "";
                    try
                    {
                        var reqObj = JObject.Parse(msg);
                        detectorName = reqObj["detectorname"]?.ToString();
                    }
                    catch
                    {                       
                        detectorName = "";
                    }

                    string responseJson = detectorName switch
                    {
                        "DummyCam1" => @"{
                                ""responsetype"": ""detectorproperties"",
                                ""detectorproperties"": [
                                    { ""descriptor"": ""Exposure time"", ""kind"": ""numeric"", ""propertycode"": 0, ""value"": 0.1 },
                                    { ""availableoptions"": [""16x16"", ""32x32"", ""64x64"", ""128x128"", ""256x256"", ""512x512"", ""1024x1024"", ""1280x1280"", ""1536x1536"", ""2048x2048""], ""current"": ""64x64"", ""descriptor"": ""Sensor cropping"", ""kind"": ""discrete"", ""propertycode"": 1 },
                                    { ""availableoptions"": [""1"", ""2"", ""4""], ""current"": ""1"", ""descriptor"": ""Binning"", ""kind"": ""discrete"", ""propertycode"": 2 }
                                ],
                                ""framerate"": 20.0
                            }",
                                        "DummyCam2" => @"{
                                ""responsetype"": ""detectorproperties"",
                                ""detectorproperties"": [
                                    { ""descriptor"": ""Exposure time"", ""kind"": ""numeric"", ""propertycode"": 0, ""value"": 0.05 },
                                    { ""availableoptions"": [""16x16"", ""32x32"", ""64x64"", ""128x128"", ""256x256"", ""512x512"", ""1024x1024"", ""1280x1280"", ""1536x1536"", ""2048x2048""], ""current"": ""1024x1024"", ""descriptor"": ""Sensor cropping"", ""kind"": ""discrete"", ""propertycode"": 1 },
                                    { ""availableoptions"": [""1"", ""2"", ""4""], ""current"": ""1"", ""descriptor"": ""Binning"", ""kind"": ""discrete"", ""propertycode"": 2 }
                                ],
                                ""framerate"": 20.0
                            }",
                                        _ => @"{
                                ""responsetype"": ""detectorproperties"",
                                ""detectorproperties"": [],
                                ""framerate"": 0.0
                            }"
                    };

                    onResponse(responseJson);
                });

            messagesMock
                   .Setup(m => m.SendDataRequest(
                       It.Is<string>(s => s.Contains("listavailableequipment")),
                       It.IsAny<string>(),
                       It.IsAny<Action<string>>(),
                       It.IsAny<Action<byte[]>>()))
                   .Callback<string, string, Action<string>, Action<byte[]>>((_, __, onResponse, ___) =>
                   {
                       onResponse(@"
                {
                ""responsetype"": ""availableequipment"",
                ""equipment"": [
                    {
                        ""name"": ""fw1"",
                        ""availablelightsources"": [],
                        ""availablemovablecomponents"": [
                            {
                                ""componentname"": ""fw"",
                                ""type"": ""discretemovablecomponent"",
                                ""possiblesettings"": [""DAPI"", ""GFP"", ""YFP"", ""RFP"", ""640""]
                            },
                            {
                                ""componentname"": ""sl"",
                                ""type"": ""continuousmovablecomponent"",
                                ""minvalue"": 0,
                                ""maxvalue"": 100,
                                ""increment"": 1
                            }
                        ],
                        ""hasmotorizedstage"": false,
                        ""motorizedstageName"": """",
                        ""hasrobot"": false,
                        ""robotname"": """"
                    },
                    {
                        ""name"": ""fw2"",
                        ""availablelightsources"": [],
                        ""availablemovablecomponents"": [
                            {
                                ""componentname"": ""fw"",
                                ""type"": ""discretemovablecomponent"",
                                ""possiblesettings"": [""DAPI/GFP/RFP/640"", ""CFP/YFP/RFP""]
                            },
                            {
                                ""componentname"": ""sl"",
                                ""type"": ""continuousmovablecomponent"",
                                ""minvalue"": 0,
                                ""maxvalue"": 100,
                                ""increment"": 1
                            }
                        ],
                        ""hasmotorizedstage"": false,
                        ""motorizedstageName"": """",
                        ""hasrobot"": false,
                        ""robotname"": """"
                    },
                    {
                        ""name"": ""hello"",
                        ""availablelightsources"": [
                            {
                                ""name"": ""ls"",
                                ""channels"": [""ch1"", ""ch2""],
                                ""allowmultiplechannels"": true,
                                ""cancontrolpower"": true
                            }
                        ],
                        ""availablemovablecomponents"": [],
                        ""hasmotorizedstage"": false,
                        ""motorizedstageName"": """",
                        ""hasrobot"": false,
                        ""robotname"": """"
                    },
                    {
                        ""name"": ""Dummy stage"",
                        ""availablelightsources"": [],
                        ""availablemovablecomponents"": [],
                        ""hasmotorizedstage"": true,
                        ""motorizedstageName"": ""dStage"",
                        ""hasrobot"": false,
                        ""robotname"": """"
                    }
                ]
            }");
        });
        }
    }
}
