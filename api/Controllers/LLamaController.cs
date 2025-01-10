using LLama.Common;
using LLama.Sampling;
using LLama;
using Microsoft.AspNetCore.Mvc;
using LLama.Abstractions;
using api.Models;
using api.Services;

namespace api.Controllers
{
    // TODO: kör [Authorize] senare i hela controllern
    [ApiController]
    [Route("[controller]")]
    public class LLamaController : ControllerBase
    {
        private readonly InteractiveExecutor _executor;
        private readonly InferenceParams _inferenceParams;
        private readonly HistoryService _historyService;

        // Store several system prompts mapped to different historical figures
        private static readonly Dictionary<string, string> _systemPrompts = new()
        {
            {
                "donald-trump",
                "<|im_start|>system\nYou are Donald Trump. Talk and act like him.\n<|im_end|>"
            },
            {
                "abraham-lincoln",
                "<|im_start|>system\nYou are Abraham Lincoln. Speak in his thoughtful, measured tone.\n<|im_end|>"
            },
            {
                "theodore-roosevelt",
                "<|im_start|>system\nYou are Theodore Roosevelt. Be bold, energetic, and use his distinct manner of speech.\n<|im_end|>"
            },
            {
                "donald-duck",
                "<|im_start|>system\nYou are Donald Duck. Be funny, angry, and use his distinct manner of speech.\n<|im_end|>"
            },
            {
                "gollum",
                "<|im_start|>system\nYou are Gollum. Be evil, sly, and use his distinct manner of speech.\n<|im_end|>"
            },
            {
                "santa",
                "<|im_start|>system\nYou are santa. Be funny, happy, and use his distinct manner of speech.\n<|im_end|>"
            },
            {
                "socrates",
                "<|im_start|>system\nYou are socrates. Use the socratic method.\n<|im_end|>"
            },
            {
                "snoop dogg",
                "<|im_start|>system\nYou are snoop dogg. Be happy, high, and use his distinct manner of speech.\n<|im_end|>"
            },
            {
                "joe",
                "<|im_start|>system\nYou are Joe Biden. Be forgetful, dement, and use his distinct manner of speech.\n<|im_end|>"
            }
        };

        // In case someone picks an ID that’s not in our dictionary, pick a default:
        private const string DefaultSystemPromptId = "donald-trump";

        public LLamaController(IConfiguration configuration, HistoryService historyService)
        {
            // Modify this to your own local path/model
            string modelPath = configuration["ModelPath"]!;
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 2048,
                GpuLayerCount = 5
            };

            var model = LLamaWeights.LoadFromFile(parameters);
            var context = model.CreateContext(parameters);
            _executor = new InteractiveExecutor(context);

            // These inference parameters can be tuned as well.
            _inferenceParams = new InferenceParams()
            {
                MaxTokens = 2048,
                AntiPrompts = new List<string> { "<|im_end|>", "User:", "Assistant:" }
            };
            _historyService = historyService;
        }

        // TODO: Byt [FromBody] Chathistory.Message till modell som finns i WPF, "Send"
        [HttpPost("send/{presidentId?}")]
        public async Task<IActionResult> SendMessage([FromRoute] string presidentId, [FromBody] Send send)
        {
            if (string.IsNullOrWhiteSpace(send.Message))
                return BadRequest("User input cannot be empty.");

            if (send.Message.Length > 2048)
                return BadRequest("User input cannot exceed 2048 characters.");

            ChatSession session = new ChatSession(_executor, new ChatHistory());
            ChatHistory history = new ChatHistory();
            if (send.SessionId != null)
            {
                history = await _historyService.GetHistoryAsync(send.SessionId);

                if (history == null)
                {
                    return NotFound("Session not found.");
                }

                session = new ChatSession(_executor, history);
            }

            // Look up the system prompt by ID, or fall back to default if not found
            if (string.IsNullOrEmpty(presidentId))
            {
                presidentId = DefaultSystemPromptId;
            }

            if (!_systemPrompts.ContainsKey(presidentId.ToLower()))
            {
                presidentId = DefaultSystemPromptId;
            }

            // Add the system prompt to the prompt text
            var systemPrompt = _systemPrompts[presidentId.ToLower()];


            // Construct history string from ChatHistory object
            var historyText = string.Join("\n", history.Messages.Select(message =>
                message.AuthorRole == AuthorRole.User ? $"<|im_start|>user\n{message.Content}<|im_end|>" : $"<|im_start|>assistant\n{message.Content}<|im_end|>"));

            Console.WriteLine(historyText);
            // Construct the final prompt with system prompt, history, and current user input
            var prompt =
                $"{systemPrompt}\n" +  // Add the system prompt
                $"{historyText}\n" +   // Include session history messages
                $"<|im_start|>user\n{send.Message}<|im_end|>\n" + // Add the new user message
                $"<|im_start|>assistant\n";  // Prepare for assistant response

            var response = string.Empty;
            await foreach (var text in _executor.InferAsync(prompt, _inferenceParams))
            {
                response += text;
            }

            // Don’t include anything after or including <|im_end|>
            response = response.Split("<|im_end|>").FirstOrDefault()?.Trim() ?? response.Trim();

            // Save the session history using HistoryService
            // TODO: error handling?
            history.AddMessage(AuthorRole.User, send.Message);
            history.AddMessage(AuthorRole.Assistant, response);

            string sessionId = send.SessionId ?? _historyService.GenerateSessionId();
            await _historyService.SaveHistoryAsync(sessionId, history);

            return Ok(new { Response = response, SessionId = sessionId });
        }

        [HttpGet("chat/{sessionId}")]
        public async Task<IActionResult> GetChatHistory(string sessionId)
        {
            var json = await _historyService.GetHistoryAsync(sessionId);

            if (json != null)
            {
                return Ok(json);
            }

            return NotFound();
        }
    }
}
