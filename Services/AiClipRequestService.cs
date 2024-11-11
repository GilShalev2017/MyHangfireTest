using ActIntelligenceService.Domain.Models.AIClip;
using System.Text.Json;
using System.Text;
using static System.Net.WebRequestMethods;
using System.Net.Http.Headers;

namespace HangfireTest.Services
{
    public class AiClipRequestService
    {
        private readonly HttpClient _httpClient;
        //private readonly string baseAiClipRequestApi = "http://cancun.local/actus5/v2/ActIntelligenceService/intelligence/api/aicliprequest";
        private readonly string baseAiClipRequestApi = "http://cancun.local/actus5/v2/ActIntelligenceService/intelligence/api/aicliprequest";

        public AiClipRequestService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> PostAiClipRequestAsync(AiClipRequest aiClipRequest)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(aiClipRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
                var response = await _httpClient.PostAsync(baseAiClipRequestApi, content);

                response.EnsureSuccessStatusCode();
                
                var responseBody = await response.Content.ReadAsStringAsync();

                //var result = JsonSerializer.Deserialize<dynamic>(responseBody);

                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);

                // Check if "Id" key exists and retrieve its value
                if (result != null && result.ContainsKey("Id"))
                {
                    var id = result["Id"].ToString();
                   
                    Console.WriteLine($"AiClip request created with Id: {id}");
                    
                    return id!;
                }
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");

                return "";
            }
        }
    }
}
