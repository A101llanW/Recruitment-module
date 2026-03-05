using System;
using HR.Web.Services;

namespace HR.Web.Test
{
    public class ScoringTest
    {
        public static void TestScoring()
        {
            var mcpService = new MCPService();
            
            Console.WriteLine("Testing MCP Service with different answers:");
            Console.WriteLine("=====================================");
            
            // Test with different answer scenarios
            var testCases = new[]
            {
                new { questionText = "What is your experience level?", answer = "Expert" },
                new { questionText = "How many years of experience?", answer = "5" },
                new { questionText = "Rate your skills", answer = "4" },
                new { questionText = "Why do you want this job?", answer = "I am very interested" }
            };
            
            foreach (var testCase in testCases)
            {
                var parameters = new
                {
                    questionText = testCase.questionText,
                    selectedAnswer = testCase.answer,
                    questionType = "choice",
                    availableOptions = new[] { "Poor", "Good", "Excellent" },
                    maxPoints = 10
                };
                
                var result = mcpService.CallToolAsync("evaluate-answer", parameters).Result;
                
                if (result.Success && result.Result != null)
                {
                    var content = result.Result.contents[0];
                    Console.WriteLine($"Question: {testCase.questionText}");
                    Console.WriteLine($"Answer: {testCase.answer}");
                    Console.WriteLine($"Score: {content.text}");
                    Console.WriteLine("---");
                }
            }
        }
    }
}
