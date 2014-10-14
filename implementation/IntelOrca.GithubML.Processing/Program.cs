using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.GithubML.Processing
{
	internal class Example
	{
		public int[] Features { get; set; }
		public bool[] Labels { get; set; }

		public override string ToString()
		{
			return String.Join(",", Labels.Select(x => x ? 1 : 0));
		}
	}

	internal class Program
	{
		private static Random _random = new Random();
		private static Example[] _trainingData;

		private static void Main(string[] args)
		{
			string[] csvFiles = Directory.GetFiles(
				@"..\..\..\..\data",
				"*.csv"
			);

			List<Example> examples = new List<Example>();
			foreach (string csvFile in csvFiles)
				examples.AddRange(GetExamples(csvFile));

			int k = 13;
			int folds = 5;
			int numRepeats = 5;
			double errorRate = 0;
			for (int repeats = 0; repeats < numRepeats; repeats++) {
				Example[][] splitExamples = examples
					.OrderBy(x => _random.Next())
					.ToArray()
					.SplitInto(folds).ToArray();

				List<Example> trainingData = new List<Example>();
				for (int i = 0; i < folds; i++) {
					trainingData.Clear();
					for (int j = 0; j < folds; j++)
						if (i != j)
							trainingData.AddRange(splitExamples[j]);

					_trainingData = trainingData.ToArray();
					errorRate += Test(splitExamples[i], k);
				}
			}
			errorRate /= numRepeats * folds;

			Console.WriteLine("Error rate: {0:0.00}", errorRate);
			Console.ReadLine();
		}

		private static double Test(Example[] examples, int k)
		{
			double errorRate = 0;
			foreach (Example example in examples) {
				Example[] closestExamples = GetClosestKExamples(example, k);
				int[] labelsTotal = new int[3];
				foreach (Example closestExample in closestExamples)
					for (int i = 0; i < 3; i++)
						labelsTotal[i] += closestExample.Labels[i] ? 1 : 0;

				bool[] labelPredictions = new bool[3];
				for (int i = 0; i < 3; i++)
					if (labelsTotal[i] > k / 2)
						labelPredictions[i] = true;

				int incorrectPredictions = 0;
				for (int i = 0; i < 3; i++)
					if (labelPredictions[i] != example.Labels[i])
						incorrectPredictions++;

				// errorRate += (incorrectPredictions / 3.0);
				errorRate += labelPredictions[0] == example.Labels[0] ? 0 : 1;
			}
			errorRate /= examples.Length;

			return errorRate;
		}

		private static Example[] GetClosestKExamples(Example example, int k)
		{
			return _trainingData
				.OrderBy(x => MeasureDistance(x, example))
				.Take(k).ToArray();
		}

		private static int MeasureDistance(Example a, Example b)
		{
			int[] aFeatures = a.Features;
			int[] bFeatures = b.Features;
			int hamming = 0;
			for (int i = 0; i < a.Features.Length; i++) {
				if (aFeatures[i] != bFeatures[i])
					hamming++;
			}
			return hamming;
		}

		private static IEnumerable<Example> GetExamples(string path)
		{
			CsvSheet sheet = new CsvSheet(path);
			for (int i = 1; i < sheet.Rows; i++) {
				Example example = new Example();

				List<int> features = new List<int>();
				List<bool> labels = new List<bool>();
				for (int j = 4; j < sheet.Columns - 3; j++)
					features.Add(Int32.Parse(sheet[j, i]));

				for (int j = sheet.Columns - 3; j < sheet.Columns; j++)
					labels.Add(Int32.Parse(sheet[j, i]) != 0);

				example.Features = features.ToArray();
				example.Labels = labels.ToArray();
				yield return example;
			}
		}
	}
}
