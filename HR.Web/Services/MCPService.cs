using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HR.Web.Services
{
    /// <summary>
    /// Simple stub implementation of MCPService for compatibility
    /// </summary>
    public class MCPService
    {
        /// <summary>
        /// Stub implementation of CallToolAsync
        /// </summary>
        public async Task<MCPResult> CallToolAsync(string toolName, object parameters)
        {
            // Return a default result for all tool calls
            await Task.Delay(100).ConfigureAwait(false); // Simulate some processing time
            
            // Generate realistic score based on answer content
            var score = 5m; // Default middle score
            if (parameters != null)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(parameters);
                    var hash = json.GetHashCode();
                    
                    // Create more sophisticated scoring based on content
                    var baseScore = Math.Abs(hash % 10); // 0-9 base
                    
                    // Apply different scoring logic based on question type and answer quality
                    if (parameters is Dictionary<string, object> dict)
                    {
                        var questionText = dict.ContainsKey("questionText") ? dict["questionText"]?.ToString() : "";
                        var answer = dict.ContainsKey("selectedAnswer") ? dict["selectedAnswer"]?.ToString() : "";
                        var questionType = dict.ContainsKey("questionType") ? dict["questionType"]?.ToString() : "";
                        var maxPoints = dict.ContainsKey("maxPoints") ? Convert.ToDecimal(dict["maxPoints"]) : 10m;
                        
                        // Score based on answer length and content quality indicators
                        if (!string.IsNullOrEmpty(answer))
                        {
                            // Longer, more detailed answers get higher scores
                            var lengthBonus = Math.Min(answer.Length / 20.0m, 3); // Max 3 points for length
                            
                            // Keywords that indicate good answers
                            var qualityKeywords = new[] { "experience", "skilled", "expert", "proficient", "advanced", "strong", "excellent", "professional", "qualified" };
                            var keywordBonus = qualityKeywords.Count(k => answer.ToLower().Contains(k.ToLower())) * 0.5m;
                            
                            // Question type specific scoring
                            switch (questionType?.ToLower())
                            {
                                case "choice":
                                    score = 3 + (baseScore % 7); // 3-9 for choice questions
                                    break;
                                case "rating":
                                    score = 2 + (baseScore % 8); // 2-9 for rating questions  
                                    break;
                                case "text":
                                    score = 5 + (baseScore % 15); // Use 5-20 as a base for text questions
                                    score += (lengthBonus * 3) + (keywordBonus * 2); // Scale bonuses to match 30pt max
                                    break;
                                default:
                                    score = 3 + (baseScore % 7); // Default 3-9
                                    break;
                            }
                            
                            // Ensure score doesn't exceed max points
                            score = Math.Min(score, maxPoints);
                        }
                    }
                    else
                    {
                        // Fallback to simple hash-based scoring
                        score = 3 + (Math.Abs(hash) % 7); // Score between 3-9
                    }
                }
                catch
                {
                    // If anything fails, use default score
                    score = 5m;
                }
            }
            
            // Create a stub response that matches expected structure
            var stubResponse = new MCPResultData
            {
                contents = new[]
                {
                    new MCPContent
                    {
                        text = JsonConvert.SerializeObject(new AnswerEvaluationResponse
                        {
                            score = score,
                            reasoning = string.Format("MCPService stub - AI evaluation not available (content-based score: {0:F1})", score),
                            confidence = 0.7m
                        })
                    }
                }
            };
            
            return new MCPResult
            {
                Success = true,
                Message = string.Format("MCPService stub response for tool '{0}'", toolName),
                Data = null,
                Result = stubResponse
            };
        }
    }

    /// <summary>
    /// Result class for MCP service calls
    /// </summary>
    public class MCPResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public MCPResultData Result { get; set; }
    }

    /// <summary>
    /// Data structure for MCP result
    /// </summary>
    public class MCPResultData
    {
        public MCPContent[] contents { get; set; }
    }

    /// <summary>
    /// Content structure for MCP result
    /// </summary>
    public class MCPContent
    {
        public string text { get; set; }
    }
}
