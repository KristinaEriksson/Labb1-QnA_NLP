using Azure;
using Azure.AI.Language.QuestionAnswering;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;


namespace Labb1_QnA_NLP
{
    internal class Program
    {
        private static SpeechConfig speechConfig;
        private static bool isSpeechMode;

        // Function to check if the text is in SSML format
        static bool IsSSML(string text)
        {
            // A simple way to determine if the text starts with "<speak>"
            return text.TrimStart().StartsWith("<speak>", StringComparison.OrdinalIgnoreCase);
        }
        static void Main(string[] args)
        {
            // Load configuration from appsettings.json
            IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

            // Extract configuration values
            string languageEndpoint = config["LanguageEndpoint"];
            string languageKey = config["LanguageKey"];
            string textAnalyticsEndpoint = config["TextAnalyticsEndpoint"];
            string textAnalyticsKey = config["TextAnalyticsKey"];
            string speechkey = config["SpeechKey"];
            string speechlocation = config["SpeechLocation"];
            string projectName = "PubgQnA";
            string deploymentName = "production";

            //Create QnA client using extracted endpoint and key
            Uri qnaEndpoint = new Uri(languageEndpoint);
            AzureKeyCredential qnaCredentials = new AzureKeyCredential(languageKey);
            QuestionAnsweringClient client = new QuestionAnsweringClient(qnaEndpoint, qnaCredentials);
            QuestionAnsweringProject project = new QuestionAnsweringProject(projectName, deploymentName);

            // Configure speech service
            speechConfig = SpeechConfig.FromSubscription(speechkey, speechlocation);
            Console.WriteLine("Ready to use speech service in " + speechConfig.Region);

            // Configure voice
            speechConfig.SpeechSynthesisLanguage = "en-US-AriaNeural";

            // Create Text Analytics client using extracted endpoint and key
            Uri cogEndpoint = new Uri(textAnalyticsEndpoint);
            AzureKeyCredential textAnalyticsCredentials = new AzureKeyCredential(textAnalyticsKey);
            TextAnalyticsClient CogClient = new TextAnalyticsClient(cogEndpoint, textAnalyticsCredentials);

            

            // Main loop
            while (true)
            {
                Console.Clear();

                // Welcome message
                Console.WriteLine("Welcome to PUBG:BATTLEGROUNDS Question and Answers.");
                Console.WriteLine();

                // Display menu
                Console.WriteLine("Choose an option: ");
                Console.WriteLine("1. Ask a question using text.");
                Console.WriteLine("2. Ask a question using speech.");
                Console.WriteLine("3. Quit");

                var choice = Console.ReadLine();

                // Handle user's choice
                switch (choice)
                {
                    case "1":
                        AskQuestionUsingText(client, project, CogClient);
                        break;
                    case "2":
                        AskQuestionUsingSpeech(client, project, CogClient);
                        break;
                    case "3":
                        Console.WriteLine("Exiting the QnA application...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please select a valid option.");
                        break;
                }
            }
        }
        static void AskQuestionUsingText(QuestionAnsweringClient client, QuestionAnsweringProject project, TextAnalyticsClient cogClient)
        {
            while (true)
            {
                Console.WriteLine();
                // Prompt the user to enter a question and provide an option to go back
                Console.WriteLine("Enter your question (type 'back' to go back to menu): ");
                var question = Console.ReadLine();

                if (question.ToLower() == "back")
                    return;
                // Process the entered question using the ProcessQuestion method
                ProcessQuestion(question, client, project, cogClient, false);
            }
            
        }

        static void AskQuestionUsingSpeech(QuestionAnsweringClient client, QuestionAnsweringProject project, TextAnalyticsClient CogClient)
        {
            while (true)
            {
                // Prompt the user to speak a question 
                Console.WriteLine("Speak your question (or say 'go back' to return to the menu.");

                // Call the RecognizeSpeech method to capture the user's spoken question
                string question = RecognizeSpeech();
                
                if(IsGoBackPhrase(question))
                {
                    Console.WriteLine("Going back to the menu...");
                    return;
                }

                // Process the recognized question using the ProcessQuestion method
                ProcessQuestion(question, client, project, CogClient, true);
            }
            
        }

        static bool IsGoBackPhrase(string phrase) 
        {
            // Define the escape phrase's here, e.g., "go back", "return to menu", etc.
            string[] escapePhrases = { "go back", "return to menu", "back to menu" };

            // Check if the provided phrase matches any of the escape phrases (case insensitive)
            foreach (var escapePhrase in escapePhrases)
            {
                if (phrase.ToLower().Contains(escapePhrase))
                {
                    return true;
                }
            }

            return false;
        }

        static string RecognizeSpeech()
        {
            // Set up audio configuration to use the default microphone input
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();

            // Initialize a speech recognizer using the configured speech settings
            using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            // Display a message indicating that the application is listening
            Console.WriteLine("Listening...");

            // Perform speech recognition on the captured audio input asyncronously
            var speechResult = speechRecognizer.RecognizeOnceAsync().GetAwaiter().GetResult();

            // Check if speech was successfully recognized
            if (speechResult.Reason == ResultReason.RecognizedSpeech)
            {
                // Return the recognized speech as text
                return speechResult.Text;
            }
            else
            {
                // Display an error message if speech recognition failed and return an empty string
                Console.WriteLine($"Speech recognition error: {speechResult.Reason}");
                return string.Empty;
            }
        }
        static void ProcessQuestion(string question, QuestionAnsweringClient client, QuestionAnsweringProject project, TextAnalyticsClient CogClient, bool isSpeechMode)
        {
            // QnA respond to user's question
            Response<AnswersResult> response = client.GetAnswers(question, project);

            // Get sentiment of the user's question
            DocumentSentiment sentimentAnalysis = CogClient.AnalyzeSentiment(question);

            // Determine mood based on sentiment
            string mood = "Neutral";
            if (sentimentAnalysis.Sentiment == TextSentiment.Positive)
                mood = "Positive";
            else if (sentimentAnalysis.Sentiment == TextSentiment.Negative)
                mood = "Negative";

            Console.Clear();
            // Print sentiment analysis result
            Console.WriteLine($"\nSentiment: {sentimentAnalysis.Sentiment}");

            // Display QnA answers
            foreach (KnowledgeBaseAnswer answer in response.Value.Answers)
            {
                Console.WriteLine($"Q:{question}");
                //Console.WriteLine($"A:{answer.Answer}");

                if (isSpeechMode && !IsSSML(answer.Answer))
                {
                    // In speech mode, use both speech synthesis and text display
                    using var synthesizer = new SpeechSynthesizer(speechConfig);
                    var result = synthesizer.SpeakTextAsync(answer.Answer).GetAwaiter().GetResult();
                    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        Console.WriteLine("Speaking Answer...");
                        Console.WriteLine($"A:{answer.Answer}");
                    }
                    else
                    {
                        Console.WriteLine($"Speech synthesis error: {result.Reason}");
                    }
                }
                else
                {
                    // In text mode or if the answer is in SSML format, display text only
                    Console.WriteLine($"A:{answer.Answer}");
                }
            }
        }

    }
}