using System;
using System.Collections.Generic;
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

		public static string[] GetFeaturesFromCsv(string path)
		{
			List<string> features = new List<string>();

			CsvSheet sheet = new CsvSheet(path);
			for (int x = 4; x < sheet.Columns - 3; x++)
				features.Add(sheet[x, 0]);

			return features.ToArray();
		}

		public static IEnumerable<Example> FromCsv(string path)
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
