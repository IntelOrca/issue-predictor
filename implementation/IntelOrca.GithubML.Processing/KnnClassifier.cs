using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.GithubML.Processing
{
	internal class KnnClassifier : IClassifier
	{
		private Example[] _trainingData;

		public int K { get; set; }
		public IReadOnlyList<double> ErrorRates { get; private set; }
		public double AverageErrorRate { get; private set; }

		public KnnClassifier(int k = 23)
		{
			if (k < 1)
				throw new ArgumentException("k must be at least 1.", "k");

			K = k;
		}

		public void Train(IEnumerable<Example> examples)
		{
			_trainingData = examples.ToArray();
		}

		public void Test(IReadOnlyCollection<Example> examples)
		{
			int k = K;

			if (examples.Count == 0)
				throw new ArgumentException("Must provide at least one example.", "examples");
			if (k < 1)
				throw new ArgumentException("k must be at least 1.", "k");

			double[] errorRates = new double[3];
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

				for (int i = 0; i < 3; i++)
					errorRates[i] += labelPredictions[i] == example.Labels[i] ? 0 : 1;
			}

			double averageErrorRate = 0;
			for (int i = 0; i < 3; i++) {
				errorRates[i] /= examples.Count;
				averageErrorRate += errorRates[i];
			}
			averageErrorRate /= 3;

			ErrorRates = errorRates;
			AverageErrorRate = averageErrorRate;
		}

		private Example[] GetClosestKExamples(Example example, int k)
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
				int weight = Program.FeatureWeights[i];
				if (weight == 0)
					continue;

				if (aFeatures[i] != bFeatures[i])
					hamming += Program.FeatureWeights[i];
			}

			// Console.Write("{0} -> {1} = {2} [", a, b, hamming);
			// for (int i = 0; i < a.Features.Length; i++)
			// 	if (aFeatures[i] != bFeatures[i])
			// 		Console.Write(Program._featureNames[i] + " | ");
			// Console.WriteLine();
			return hamming;
		}
	}
}
