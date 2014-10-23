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
		public static string[] _featureNames;

		public static int[] FeatureWeights;

		private static void Main(string[] args)
		{
			string[] csvFiles = Directory.GetFiles(
				@"..\..\..\..\data",
				"*.csv"
			);

			_featureNames = GetFeatures(csvFiles[0]);

			List<Example> examples = new List<Example>();
			foreach (string csvFile in csvFiles)
				examples.AddRange(GetExamples(csvFile));

			// var filteredExamples = examples.Where(x => x.Labels.Contains(true)).ToArray();
			// examples.Clear();
			// examples.AddRange(filteredExamples);

			FeatureWeights = Enumerable.Range(0, examples[0].Features.Length)
				.Select(x => 1)
				.ToArray();

			RankByMutalInformation(examples.ToArray());
			// return;

			KnnClassifier knn = new KnnClassifier(23);
			double errorRate = TestNTimes(knn, examples, 5, 5);

			Console.WriteLine("Error rate: {0:0.00}", errorRate);
			Console.ReadLine();
		}

		private static void RankByMutalInformation(Example[] examples)
		{
			int numFeatures = examples[0].Features.Length;
			FeatureWeights = new int[numFeatures];

			// var filteredExamples = examples.Where(x => x.Labels.Contains(true)).ToArray();

			double[] probability = new double[numFeatures];
			double[] mutualInformation = new double[numFeatures];
			for (int i = 0; i < numFeatures; i++) {
				probability[i] = GetProbability(examples, i, 0);
				mutualInformation[i] = MutualInformation(examples, i, 0);

				// var meh = examples.Where(x => x.Features[i] != 0).ToArray();
				// mutualInformation[i] = meh.Length == 0 ?
				// 	Double.NaN :
				// 	meh.Average(x => x.Labels[0] ? 1 : 0);
				// 
				FeatureWeights[i] = 0;
			}

			var ranking = Enumerable.Range(0, numFeatures)
				.Select(i => new { Feature = i, MI = mutualInformation[i] })
				.OrderBy(x => x.MI)
				.ToArray();

			for (int i = 0; i < ranking.Length; i++) {
				var r = ranking[i];
				FeatureWeights[r.Feature] = (int)(r.MI * 100);
				if (r.MI < 0.5)
					FeatureWeights[r.Feature] = 0;
			}

			int wtf = FeatureWeights.Count(x => x != 0);
			Console.WriteLine(wtf);

			// FeatureWeights[i] = 0;
			// if (!Double.IsNaN(mutualInformation[i])) {
			// 	FeatureWeights[i] = (int)(Math.Pow(mutualInformation[i], 4) * 1000);
			// 	// FeatureWeights[i] = (int)(mutualInformation[i] * 1000);
			// }

			// for (int i = 0; i < numFeatures; i++)
			// 	Console.WriteLine("{0},{1:0.000},{2:0.000}", _featureNames[i], probability[i], mutualInformation[i]);
			// Console.ReadLine();
		}

		private static double GetProbability(IReadOnlyCollection<Example> examples, int feature, int label)
		{
			var examplesGivenX = examples
				.Where(x => x.Features[feature] != 0)
				.ToArray();

			if (examplesGivenX.Length == 0)
				return Double.NaN;

			return examplesGivenX.Average(x => x.Labels[label] ? 1 : 0);
		}

		private static double MutualInformation(IReadOnlyCollection<Example> examples, int feature, int label)
		{
			// X: feature
			// Y: label

			examples = examples
				.Where(x => x.Labels[0] || x.Labels[1] || x.Labels[2])
				.ToArray();

			var examplesGivenX = examples
				.Where(x => x.Features[feature] != 0)
				.ToArray();

			double pY = examples.Average(x => x.Labels[label] ? 1 : 0);
			double pYgivenX = GetProbability(examples, feature, label);
			// double pYgivenNotX = examplesGivenX.Average(x => x.Labels[label] ? 1 : 0);

			if (Double.IsNaN(pYgivenX))
				return Double.NaN;

			double hY = BinaryEntropy(pY, 2);
			double hYgivenX = BinaryEntropy(pYgivenX, 2);
			// double hYgivenNotX = BinaryEntropy(pYgivenNotX, 2);

			return hY - hYgivenX;
		}

		private static double BinaryEntropy(double p, double logBase)
		{
			double notP = 1 - p;
			return -((p * Math.Log(p, logBase)) + (notP * Math.Log(notP, logBase)));
		}

		private static double TestNTimes(IClassifier classifier, IEnumerable<Example> examples, int n, int folds)
		{
			double errorRate = 0;
			for (int repeats = 0; repeats < n; repeats++)
				errorRate += CrossValidate(classifier, examples, folds);

			return errorRate / n;
		}

		private static double CrossValidate(IClassifier classifier, IEnumerable<Example> examples, int folds)
		{
			double errorRate = 0;

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

				classifier.Train(trainingData);
				classifier.Test(splitExamples[i]);

				errorRate += classifier.ErrorRates[0];
				// errorRate += classifier.AverageErrorRate;
			}
			return errorRate / folds;
		}

		private static string[] GetFeatures(string path)
		{
			List<string> features = new List<string>();

			CsvSheet sheet = new CsvSheet(path);
			for (int x = 4; x < sheet.Columns - 3; x++)
				features.Add(sheet[x, 0]);

			return features.ToArray();
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
