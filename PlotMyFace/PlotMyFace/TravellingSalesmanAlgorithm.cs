// TravellingSalesmanAlgorithm.cs
//
// Copyright (C) Paulo Zemek
// Original from: http://www.codeproject.com/Articles/792887/Travelling-Salesman-Genetic-Algorithm

// The MIT License(MIT)
// Copyright(c) 2015 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlotMyFace
{
	public sealed class TravellingSalesmanAlgorithm
	{
		private readonly Location _startLocation;
		private readonly KeyValuePair<Location[], double>[] _populationWithDistances;

		public TravellingSalesmanAlgorithm(Location startLocation, Location[] destinations, int populationCount)
		{
			if (startLocation == null)
				throw new ArgumentNullException("startLocation");

			if (destinations == null)
				throw new ArgumentNullException("destinations");

			if (populationCount < 2)
				throw new ArgumentOutOfRangeException("populationCount");

			if (populationCount % 2 != 0)
				throw new ArgumentException("The populationCount parameter must be an even value.", "populationCount");

			_startLocation = startLocation;
			destinations = (Location[])destinations.Clone();

			foreach(var destination in destinations)
				if (destination == null)
					throw new ArgumentException("The destinations array can't contain null values.", "destinations");

			// This commented method uses a search of the kind "look for the nearest non visited location".
			// This is rarely the shortest path, yet it is already a "somewhat good" path.
			destinations = _GetFakeShortest(destinations);

			_populationWithDistances = new KeyValuePair<Location[], double>[populationCount];

			// Create initial population.
			for(int solutionIndex=0; solutionIndex<populationCount; solutionIndex++)
			{
				var newPossibleDestinations = (Location[])destinations.Clone();

				// Try commenting the next 2 lines of code while keeping the _GetFakeShortest active.
				// If you avoid the algorithm from running and press reset, you will see that it always
				// start with a path that seems "good" but is not the best.
				//for(int randomIndex=0; randomIndex<newPossibleDestinations.Length; randomIndex++)
					//RandomProvider.FullyRandomizeLocations(newPossibleDestinations);

				var distance = Location.GetTotalDistance(startLocation, newPossibleDestinations);
				var pair = new KeyValuePair<Location[], double>(newPossibleDestinations, distance);

				_populationWithDistances[solutionIndex] = pair;
			}

			Array.Sort(_populationWithDistances, _sortDelegate);
		}

		private Location[] _GetFakeShortest(Location[] destinations)
		{
			Location[] result = new Location[destinations.Length];

			var currentLocation = _startLocation;
			for(int fillingIndex=0; fillingIndex<destinations.Length; fillingIndex++)
			{
				int bestIndex = -1;
				double bestDistance = double.MaxValue;

				for(int evaluatingIndex=0; evaluatingIndex<destinations.Length; evaluatingIndex++)
				{
					var evaluatingItem = destinations[evaluatingIndex];
					if (evaluatingItem == null)
						continue;

					double distance = currentLocation.GetDistance(evaluatingItem);
					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestIndex = evaluatingIndex;
					}
				}

				result[fillingIndex] = destinations[bestIndex];
				currentLocation = destinations[bestIndex];
				destinations[bestIndex] = null;
			}

			return result;
		}

		private static readonly Comparison<KeyValuePair<Location[], double>> _sortDelegate = _Sort;
		private static int _Sort(KeyValuePair<Location[], double> value1, KeyValuePair<Location[], double> value2)
		{
			return value1.Value.CompareTo(value2.Value);
		}

		public IEnumerable<Location> GetBestSolutionSoFar()
		{
			foreach(var location in _populationWithDistances[0].Key)
				yield return location;
		}

		public bool MustMutateFailedCrossovers { get; set; }
		public bool MustDoCrossovers { get; set; }

		public void Reproduce()
		{
			var bestSoFar = _populationWithDistances[0];

			int halfCount = _populationWithDistances.Length / 2;
			for(int i=0; i<halfCount; i++)
			{
				var parent = _populationWithDistances[i].Key;
				var child1 = _Reproduce(parent);
				var child2 = _Reproduce(parent);

				var pair1 = new KeyValuePair<Location[], double>(child1, Location.GetTotalDistance(_startLocation, child1));
				var pair2 = new KeyValuePair<Location[], double>(child2, Location.GetTotalDistance(_startLocation, child2));
				_populationWithDistances[i*2] = pair1;
				_populationWithDistances[i*2 + 1] = pair2;
			}

			// We keep the best alive from one generation to the other.
			_populationWithDistances[_populationWithDistances.Length-1] = bestSoFar;

			Array.Sort(_populationWithDistances, _sortDelegate);
		}

		public void MutateDuplicates()
		{
			bool needToSortAgain = false;
			int countDuplicates = 0;

			var previous = _populationWithDistances[0];
			for(int i=1; i<_populationWithDistances.Length; i++)
			{
				var current = _populationWithDistances[i];
				if (!previous.Key.SequenceEqual(current.Key))
				{
					previous = current;
					continue;
				}

				countDuplicates++;

				needToSortAgain = true;
				RandomProvider.MutateRandomLocations(current.Key);
				_populationWithDistances[i] = new KeyValuePair<Location[], double>(current.Key, Location.GetTotalDistance(_startLocation, current.Key));
			}

			if (needToSortAgain)
				Array.Sort(_populationWithDistances, _sortDelegate);
		}

		private Location[] _Reproduce(Location[] parent)
		{
			var result = (Location[])parent.Clone();

			if (!MustDoCrossovers)
			{
				// When we are not using cross-overs, we always apply mutations.
				RandomProvider.MutateRandomLocations(result);
				return result;
			}

			// if you want, you can ignore the next three lines of code and the next
			// if, keeping the call to RandomProvider.MutateRandomLocations(result); always
			// invoked and without crossovers. Doing that you will not promove evolution through
			// "sexual reproduction", yet the good result will probably be found.
			int otherIndex = RandomProvider.GetRandomValue(_populationWithDistances.Length/2);
			var other = _populationWithDistances[otherIndex].Key;
			RandomProvider._CrossOver(result, other, MustMutateFailedCrossovers);

			if (!MustMutateFailedCrossovers)
				if (RandomProvider.GetRandomValue(10) == 0)
					RandomProvider.MutateRandomLocations(result);

			return result;
		}
	}
}
