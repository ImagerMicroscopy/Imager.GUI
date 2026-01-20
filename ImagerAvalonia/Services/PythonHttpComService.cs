using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services
{
    public interface IPythonComService
    {
        public Task<string> SetUpAvailableNodes();
        public Task<string> GetNodeInfo(string path);
        public Task<HttpResponseMessage> SubmitNodes(JArray dag_nodes);
        public Task<HttpResponseMessage> SubmitDags(JArray serializedDags);
        public Task<string> SubmitFolder(string path);
        public Task<string> ReloadFile(string path);
        public Task<string> GetMethods(string selectedProgram);
        public Task<string> GetParameters(string selectedProgram);
        public Task<string> GetAcquisitionUpdates(string selectedProgram);
        public Task<string> GetUpdateAcqParameters(string selectedProgram);
        public Task<string> GetCompletions(string code, int line, int column, string? path = null);
        public Task<string> GetSignatures(string code, int line, int column, string? path = null);
        public Task<string> GetHover(string code, int line, int column, string? path = null);
        public Task<string> GetGoto(string code, int line, int column, string? path = null);
        public Task<string> GetDiagnostics(string code, string? path = null);
        public Task<string> FormatCode(string code);
        public Task<string> RenameSymbol(string code, int line, int column, string newName, string? path = null);
    }


    public class PythonHttpComService : IPythonComService
    {

        private readonly string _pythonAdress = "http://127.0.0.1:5100/";
        private readonly HttpClient _httpClient;

        public PythonHttpComService(HttpClient httpClient)
        {

            _httpClient = httpClient;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            _httpClient.DefaultRequestVersion = HttpVersion.Version11;
            _httpClient.BaseAddress = new Uri(_pythonAdress);

        }

        public async Task<HttpResponseMessage> SubmitNodes(JArray dag_nodes)
        {
            var stringContent = new StringContent(dag_nodes.ToString(), Encoding.UTF8, "application/json");
            var response = await Task.Run(() => _httpClient.PostAsync("nodesubmission/submit_dag", stringContent));
            return response;
        }


        public async Task<string> SetUpAvailableNodes()
        {
            var available_nodes = await _httpClient.GetStringAsync("dagnodes/NodeInfo/get_nodes");
            return available_nodes;
        }

        public async Task<string> GetNodeInfo(string api_path)
        {
            JObject routePath = new JObject(new JProperty("route", api_path));
            var payload = new StringContent(routePath.ToString(), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("dagnodes/NodeInfo/get_node_params", payload);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();


            return responseContent;
        }

        public async Task<HttpResponseMessage> SubmitDags(JArray serializedDags)
        {
            var stringContent = new StringContent(serializedDags.ToString(), Encoding.UTF8, "application/json");
            var response = await Task.Run(() => _httpClient.PostAsync("nodesubmission/set_dags", stringContent));
            return response;
        }


        public async Task<string> SubmitFolder(string folder_path)
        {
            var data = new
            {
                path = folder_path
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("submission/set_folder", content);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ReloadFile(string folder_path)
        {
            var data = new
            {
                path = folder_path
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("submission/reload_file", content);

            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> GetMethods(string? selectedProgram = null)
        {
            var url = "smartprogram/get_methods";
            if (!string.IsNullOrEmpty(selectedProgram))
            {
                url += $"?smartprogramname={Uri.EscapeDataString(selectedProgram)}";
            }

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStringAsync();


            return stream;
        }
        public async Task<string> GetAcquisitionUpdates(string? selectedProgram = null)
        {
            var url = "smartprogram/get_acquisition_updates";
            if (!string.IsNullOrEmpty(selectedProgram))
            {
                url += $"?smartprogramname={Uri.EscapeDataString(selectedProgram)}";
            }

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStringAsync();


            return stream;
        }
        public async Task<string> GetParameters(string? selectedProgram = null)
        {
            var url = "smartprogram/get_parameters";
            if (!string.IsNullOrEmpty(selectedProgram))
            {
                url += $"?smartprogramname={Uri.EscapeDataString(selectedProgram)}";
            }

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStringAsync();


            return stream;
        }

        public async Task<string> GetUpdateAcqParameters(string? selectedProgram = null)
        {
            var url = "smartprogram/get_acq_updates";
            if (!string.IsNullOrEmpty(selectedProgram))
            {
                url += $"?smartprogramname={Uri.EscapeDataString(selectedProgram)}";
            }

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStringAsync();


            return stream;
        }

        public async Task<string> GetCompletions(string code, int line, int column, string? path = null)
        {
            var data = new
            {
                code = code,
                line = line,
                column = column,
                path = path
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/complete", content);
                if (!response.IsSuccessStatusCode) return "[]"; 
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return "[]";
            }
        }

        public async Task<string> GetSignatures(string code, int line, int column, string? path = null)
        {
             var data = new
            {
                code = code,
                line = line,
                column = column,
                path = path
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/signatures", content);
                if (!response.IsSuccessStatusCode) return "[]"; 
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return "[]";
            }
        }

        public async Task<string> GetHover(string code, int line, int column, string? path = null)
        {
            var data = new { code, line, column, path };
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/hover", content);
                if (!response.IsSuccessStatusCode) return "[]"; 
                return await response.Content.ReadAsStringAsync();
            }
            catch { return "[]"; }
        }

        public async Task<string> GetGoto(string code, int line, int column, string? path = null)
        {
            var data = new { code, line, column, path };
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/goto", content);
                if (!response.IsSuccessStatusCode) return "[]"; 
                return await response.Content.ReadAsStringAsync();
            }
            catch { return "[]"; }
        }

        public async Task<string> GetDiagnostics(string code, string? path = null)
        {
            var data = new { code, line=1, column=0, path }; // line/col dummy
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/diagnostics", content);
                if (!response.IsSuccessStatusCode) return "[]"; 
                return await response.Content.ReadAsStringAsync();
            }
            catch { return "[]"; }
        }

        public async Task<string> FormatCode(string code)
        {
            var data = new { code, line=1, column=0 }; 
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            {
                var response = await _httpClient.PostAsync("completion/format", content);
                if (!response.IsSuccessStatusCode) return null; 
                return await response.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }

        public async Task<string> RenameSymbol(string code, int line, int column, string newName, string? path = null)
        {
             var data = new { code, line, column, new_name=newName, path };
             var json = JsonConvert.SerializeObject(data);
             var content = new StringContent(json, Encoding.UTF8, "application/json");
             try 
             {
                 var response = await _httpClient.PostAsync("completion/rename", content);
                 if (!response.IsSuccessStatusCode) return "[]"; 
                 return await response.Content.ReadAsStringAsync();
             }
             catch { return "[]"; }
        }
    }
}
