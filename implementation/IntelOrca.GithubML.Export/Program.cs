using Octokit;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelOrca.GithubML.Export
{
	internal class Program
	{
		private static HashSet<string> BugWords = new HashSet<string>() {
			// 1 word most frequent
			"0", "1", "also", "bug", "can", "change", "code", "doesn't", "don't", "error", "example", "first", "get", "i'm",
			"instead", "just", "like", "line", "name", "new", "now", "one", "open", "option", "read", "reproduce", "right",
			"show", "since", "something", "still", "time", "try", "used", "using", "version", "way", "will", "work", "works",

			// 2 word most frequent
			"a new", "and the", "does not", "if the", "if you", "in the", "is a", "of the", "on the", "that the", "the first",
			"the new", "the same", "this is", "to be", "to the", "with the",

			// Personal additions
			"always", "break", "broken", "but", "can't", "crash", "crashes", "crashed", "cause", "exception", "fail", "none",
			"not", "nothing", "tried", "odd", "should", "wrong"
		};

		private static HashSet<string> SuggestionWords = new HashSet<string>() {
			// 1 word most frequent
			"can", "currently", "just", "like", "make", "since",

			// 2 word most frequent
			"in the", "of the", "to make", "to the",

			// Personal additions
			"another", "can", "change", "cool", "could", "currently", "good", "enhance", "enhancement", "feature", "idea",
			"like", "just", "make", "many", "more", "new", "nice", "thought", "should", "suggest", "suggestion", "will", "would"
		};

		private static void Main(string[] args)
		{
			// string[] words = File.ReadAllLines("merge_questionFreq1.txt").Where(x => !StopWords.Check(x)).ToArray();
			// foreach (string word in words)
			// 	Console.WriteLine(word);
			// Console.ReadLine();


			// MergeData();
			MainAsync(args).Wait();
		}

		private static async Task MainAsync(string[] args)
		{
			string userName = "intelorca";
			string repositoryName = "openrct2";
			string bugLabel = "bug";
			string suggestionLabel = "enhancement";
			string questionLabel = "question";

			/*
			string userName = "microsoft";
			string repositoryName = "typescript";
			string bugLabel = "bug";
			string suggestionLabel = "suggestion";
			string questionLabel = "question";
			*/

			/*
			string userName = "handsontable";
			string repositoryName = "jquery-handsontable";
			string bugLabel = "bug";
			string suggestionLabel = "feature";
			string questionLabel = "question";
			*/

			// If encoded as base64
			// credentialsPassword = Encoding.UTF8.GetString(Convert.FromBase64String(credentialsPassword));

			using (StreamWriter sw = new StreamWriter(new FileStream("output.csv", System.IO.FileMode.Create, FileAccess.Write))) {
				GitHubClient github = new GitHubClient(new ProductHeaderValue("GithubMLDataExport"));

				try {
					string credentialsUserName = ConfigurationManager.AppSettings["user"];
					string credentialsPassword = ConfigurationManager.AppSettings["password"];
					github.Credentials = new Credentials(
						credentialsUserName,
						credentialsPassword
					);
				} catch { }

				Repository repo = await github.Repository.Get(userName, repositoryName);

				Console.WriteLine("Downloading issues...");
				IReadOnlyList<Issue> issues = await GetIssues(github, userName, repositoryName);

				Console.WriteLine("Exporting data...");

				List<object> columns = new List<object>();
				string[] wordFeatures = BugWords.Concat(SuggestionWords).Distinct().OrderBy(x => x).ToArray();

				// Column headings
				columns.Add("issue");
				columns.Add("user");
				columns.Add("comments");
				columns.Add("body length");
				foreach (string word in wordFeatures)
					columns.Add(word);
				columns.Add("bug");
				columns.Add("suggestion");
				columns.Add("question");
				await sw.WriteLineAsync(String.Join(",", columns));

				foreach (Issue issue in issues) {
					string[] labels = issue.Labels.Select(x => x.Name.ToLower()).ToArray();
					if (labels.Length == 0)
						continue;

					bool isBug = labels.Contains(bugLabel);
					bool isSuggestion = labels.Contains(suggestionLabel);
					bool isQuestion = labels.Contains(questionLabel);

					// Get the words that are present in the body
					string body = ProcessIssueBody(issue.Body);
					string[] wordsPresent = GetPhrases(1, body).Concat(GetPhrases(2, body)).ToArray();

					// Cross reference them with our word features
					bool[] wordFeaturePresent = new bool[wordFeatures.Length];
					for (int i = 0; i < wordFeaturePresent.Length; i++) {
						if (wordsPresent.Contains(wordFeatures[i]))
							wordFeaturePresent[i] = true;
					}

					// Row
					columns.Clear();
					columns.Add(issue.Number);
					columns.Add(issue.User.Login);
					columns.Add(issue.Comments);
					columns.Add(issue.Body.Length);
					foreach (bool b in wordFeaturePresent)
						columns.Add(b ? 1 : 0);
					columns.Add(isBug ? 1 : 0);
					columns.Add(isSuggestion ? 1 : 0);
					columns.Add(isQuestion ? 1 : 0);
					await sw.WriteLineAsync(String.Join(",", columns));
				}

				/*
				for (int i = 1; i <= 3; i++) {
					GetPhraseFrequency("bugFreq" + i + ".csv", i, issues.Where(x => IssueHasLabel(x, bugLabel)).Select(x => x.Body));
					GetPhraseFrequency("suggestionFreq" + i + ".csv", i, issues.Where(x => IssueHasLabel(x, suggestionLabel)).Select(x => x.Body));
					GetPhraseFrequency("questionFreq" + i + ".csv", i, issues.Where(x => IssueHasLabel(x, questionLabel)).Select(x => x.Body));
				}
				*/
			}
		}

		private static void MergeData()
		{
			foreach (string label in new[] { "bug", "question", "suggestion" }) {
				for (int i = 1; i <= 3; i++) {
					List<HashSet<string>> phrasesList = new List<HashSet<string>>();
					foreach (string folder in new[] { "handsontable", "openrct2", "typescript" }) {
						HashSet<string> phrases = new HashSet<string>();
						CsvSheet sheet = new CsvSheet(folder + "\\" + label + "Freq" + i + ".csv");
						for (int y = 0; y < sheet.Rows; y++) {
							string phrase = sheet[0, y];
							int frequency = Int32.Parse(sheet[1, y]);

							if (frequency <= 4)
								continue;

							phrases.Add(phrase);
						}
						phrasesList.Add(phrases);
					}
					string[] phrasesFinal = phrasesList[0].Intersect(phrasesList[1]).Intersect(phrasesList[2]).OrderBy(x => x).ToArray();
					File.WriteAllLines("merge_" + label + "Freq" + i + ".txt", phrasesFinal);
				}
			}
		}

		private static bool IssueHasLabel(Issue issue, string label)
		{
			return issue.Labels.Select(x => x.Name.ToLower()).Contains(label);
		}

		private static async Task<IReadOnlyList<Issue>> GetIssues(GitHubClient github, string userName, string repositoryName)
		{
			RepositoryIssueRequest request = new RepositoryIssueRequest();
			request.State = ItemState.Open;
			IReadOnlyList<Issue> openIssues = await github.Issue.GetForRepository(userName, repositoryName, request);
			request.State = ItemState.Closed;
			IReadOnlyList<Issue> closedIssues = await github.Issue.GetForRepository(userName, repositoryName, request);
			return openIssues.Concat(closedIssues).ToArray();
		}

		private static void GetPhraseFrequency(string path, int numWords, IEnumerable<string> bodies)
		{
			Dictionary<string, int> phraseFrequency = new Dictionary<string, int>();
			foreach (string body in bodies) {
				foreach (string phrase in GetPhrases(numWords, body)) {
					int frequency;
					phraseFrequency.TryGetValue(phrase, out frequency);
					phraseFrequency[phrase] = frequency + 1;
				}				
			}

			StringBuilder sb = new StringBuilder();
			foreach (var wf in phraseFrequency.OrderByDescending(x => x.Value)) {
				sb.AppendLine(wf.Key + "," + wf.Value);
			}
			File.WriteAllText(path, sb.ToString());
		}

		private static string[] GetPhrases(int numWords, string body)
		{
			body = ProcessIssueBody(body);
			string[] words = body
				.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				.Where(x => x.Length <= 12)
				.ToArray();

			List<string> phrases = new List<string>();
			for (int i = 0; i <= words.Length - numWords; i++)
				phrases.Add(String.Join(" ", words.Skip(i).Take(numWords)));
			return phrases.Distinct().ToArray();
		}

		private static string ProcessIssueBody(string body)
		{
			StringBuilder sb = new StringBuilder();
			bool lastCharWasWhitespace = true;
			foreach (char c in body) {
				if (Char.IsLetterOrDigit(c)) {
					sb.Append(Char.ToLower(c));
					lastCharWasWhitespace = false;
				} else if (Char.IsWhiteSpace(c)) {
					if (!lastCharWasWhitespace) {
						sb.Append(' ');
						lastCharWasWhitespace = true;
					}
				}
			}
			return sb.ToString();
		}
	}
}
