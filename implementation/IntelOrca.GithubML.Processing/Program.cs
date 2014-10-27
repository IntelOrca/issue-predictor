using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelOrca.GithubML.Processing
{
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

			_featureNames = Example.GetFeaturesFromCsv(csvFiles[0]);

			List<Example> examples = new List<Example>();
			foreach (string csvFile in csvFiles)
				examples.AddRange(Example.FromCsv(csvFile));

			// var filteredExamples = examples.Where(x => x.Labels.Contains(true)).ToArray();
			// examples.Clear();
			// examples.AddRange(filteredExamples);

			FeatureWeights = Enumerable.Range(0, examples[0].Features.Length)
				.Select(x => 1)
				.ToArray();

			Example[] filteredExamples = examples.ToArray();
			
			int label = 0;

			FeatureWeights = WrapperMethod(examples, label);

			KnnClassifier knn = new KnnClassifier(23);
			for (double min = 0; min < 1.0; min += 0.1) {
				// RankByMutalInformation(filteredExamples, label, min);
				Tuple<double, double> meanStd = TestNTimes(knn, examples, 5, 5, label);

				Console.WriteLine("Error rate: mean {0:0.00}, std {1:0.00} for {2:0.00}", meanStd.Item1, meanStd.Item2, min);
			}

			// CMIM(filteredExamples);
			// return;			
			Console.ReadLine();
		}

		private static int[] WrapperMethod(IEnumerable<Example> examples, int label)
		{
			int numFeatures = _featureNames.Length;
			KnnClassifier knn = new KnnClassifier(23);

			Queue<int> randomFeatureList = new Queue<int>(
				Enumerable.Range(0, numFeatures).OrderBy(x => _random.Next())
			);

			int[] featuresToInclude = FeatureWeights = new int[numFeatures];
			for (int i = 0; i < numFeatures; i++)
				featuresToInclude[i] = 1;

			double bestErrorRate = CrossValidate(knn, examples, 5, label);

			Stopwatch sw = new Stopwatch();
			sw.Start();
			while (randomFeatureList.Count > 0) {
				int featureIndex = randomFeatureList.Dequeue();
				featuresToInclude[featureIndex] = 0;

				double errorRate = CrossValidate(knn, examples, 5, label);
				if (errorRate < bestErrorRate)
					bestErrorRate = errorRate;
				else if (errorRate > bestErrorRate + 4)
					featuresToInclude[featureIndex] = 1;

				double remainingFeatureCount = randomFeatureList.Count;
				double featuresDoneSoFar = numFeatures - remainingFeatureCount;
				double avgTimePerTest = sw.ElapsedMilliseconds / featuresDoneSoFar;
				double eta = avgTimePerTest * remainingFeatureCount;

				Console.Clear();
				Console.WriteLine("{0:0.0}%, ETA: {1:0.0} minutes", featuresDoneSoFar / numFeatures * 100, eta / (1000 * 60));
			}

			for (int i = 0; i < numFeatures; i++)
				Console.WriteLine(featuresToInclude[i]);
			Console.ReadLine();

			return featuresToInclude.ToArray();
		}

		#region Testing / validation

		private static Tuple<double, double> TestNTimes(IClassifier classifier, IEnumerable<Example> examples, int n, int folds, int label)
		{
			double[] errorRates = Enumerable.Range(0, n)
				.Select(x => CrossValidate(classifier, examples, folds, label))
				.ToArray();

			double mean = errorRates.Average();
			double std = Math.Sqrt(errorRates.Select(x => Math.Pow(x - mean, 2)).Average());

			return new Tuple<double, double>(mean, std);
		}

		private static double CrossValidate(IClassifier classifier, IEnumerable<Example> examples, int folds, int label)
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

				errorRate += label == -1 ?
					classifier.AverageErrorRate :
					classifier.ErrorRates[label];
			}
			return errorRate / folds;
		}

		#endregion

		#region Feature selection

		private static void RankByMutalInformation(Example[] examples, int label, double minimumMutualInformation)
		{
			int numFeatures = examples[0].Features.Length;
			FeatureWeights = new int[numFeatures];

			// var filteredExamples = examples.Where(x => x.Labels.Contains(true)).ToArray();

			double[] probability = new double[numFeatures];
			double[] mutualInformation = new double[numFeatures];
			for (int i = 0; i < numFeatures; i++) {
				probability[i] = GetProbability(examples, i, label);
				mutualInformation[i] = MutualInformation(examples, i, label);

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
				FeatureWeights[r.Feature] = Double.IsNaN(r.MI) || r.MI < minimumMutualInformation ?
					0 :
					(int)(r.MI * 10000);
			}

			int numUsedFeatures = FeatureWeights.Count(x => x != 0);
			// Console.WriteLine(numUsedFeatures);

			// FeatureWeights[i] = 0;
			// if (!Double.IsNaN(mutualInformation[i])) {
			// 	FeatureWeights[i] = (int)(Math.Pow(mutualInformation[i], 4) * 1000);
			// 	// FeatureWeights[i] = (int)(mutualInformation[i] * 1000);
			// }

			// for (int i = 0; i < numFeatures; i++)
			// 	Console.WriteLine("{0},{1:0.000},{2:0.000}", _featureNames[i], probability[i], mutualInformation[i]);
			// Console.ReadLine();
		}

		private static void CMIM(Example[] examples)
		{
			int numFeatures = examples[0].Features.Length;
			FeatureWeights = new int[numFeatures];

			double[] score = new double[numFeatures];
			for (int i = 0; i < numFeatures; i++)
				score[i] = MutualInformation(examples, i, 0);

			int[] nu = new int[numFeatures];
			for (int k = 0; k < numFeatures; k++) {
				double maxScore = score[0];
				nu[k] = 0;
				for (int i = 1; i < numFeatures; i++) {
					if (score[i] > maxScore) {
						maxScore = score[i];
						nu[k] = i;
					}
				}

				for (int i = 0; i < numFeatures; i++)
					score[i] = Math.Min(score[i], ConditionalMutualInformation(examples, i, nu[k], 0));
			}

			var ranking = Enumerable.Range(0, numFeatures)
				.Select(i => new { Feature = i, CMIM = score[i] })
				.Where(x => !Double.IsNaN(x.CMIM))
				.OrderBy(x => x.CMIM)
				.ToArray();

			for (int i = 0; i < ranking.Length; i++) {
				var r = ranking[i];
				FeatureWeights[r.Feature] = i * i; // (int)(r.CMIM * 10000);
				// if (r.CMIM < 0.5)
				//	FeatureWeights[r.Feature] = 0;
			}

			int numUsedFeatures = FeatureWeights.Count(x => x != 0);
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

		private static double GetProbability(IReadOnlyCollection<Example> examples, int featureA, int featureB, int label)
		{
			var examplesGivenX = examples
				.Where(x => x.Features[featureA] != 0)
				.Where(x => x.Features[featureB] != 0)
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

		private static double ConditionalMutualInformation(IReadOnlyCollection<Example> examples, int featureA, int featureB, int label)
		{
			// I(Xa|Xb;Y) = H(Y) - H(Y|(Xa|Xb))

			// p(Y)
			// p(Y|(Xa|Xb))
			double pY = examples.Average(x => x.Labels[label] ? 1 : 0);
			double pYGivenXaGivenXb = GetProbability(examples, featureA, featureB, 0);

			return BinaryEntropy(pY, 2) - BinaryEntropy(pYGivenXaGivenXb, 2);
		}

		private static double BinaryEntropy(double p, double logBase)
		{
			double notP = 1 - p;
			return -((p * Math.Log(p, logBase)) + (notP * Math.Log(notP, logBase)));
		}

		#endregion
	}
}
