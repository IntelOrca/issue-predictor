using System.Collections.Generic;

namespace IntelOrca.GithubML.Processing
{
	internal interface IClassifier
	{
		IReadOnlyList<double> ErrorRates { get; }
		double AverageErrorRate { get; }

		void Train(IEnumerable<Example> examples);
		void Test(IReadOnlyCollection<Example> examples);
	}
}
