using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services
{
    public interface IPythonLinting
    {
        public Task<string> GetCompletions(string code, int line, int column, string? path = null);
        public Task<string> GetSignatures(string code, int line, int column, string? path = null);
        public Task<string> GetHover(string code, int line, int column, string? path = null);
        public Task<string> GetGoto(string code, int line, int column, string? path = null);
        public Task<string> GetDiagnostics(string code, string? path = null);
        public Task<string> FormatCode(string code);
        public Task<string> RenameSymbol(string code, int line, int column, string newName, string? path = null);
    }


    internal class PythonLintingService : IPythonLinting
    {

        private readonly string _pythonAdress = "http://127.0.0.1:5100/";
        private readonly HttpClient _httpClient;

        public PythonLintingService(HttpClient httpClient)
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
            var data = new { code, line = 1, column = 0, path }; // line/col dummy
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
            var data = new { code, line = 1, column = 0 };
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
            var data = new { code, line, column, new_name = newName, path };
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
