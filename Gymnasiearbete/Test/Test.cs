﻿using Gymnasiearbete.Pathfinding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Gymnasiearbete.Pathfinding.GraphOptimization;
using static Gymnasiearbete.Pathfinding.Pathfinder;

namespace Gymnasiearbete.Test
{
    static class ResultSetup
    {
        public static TestResult SetupTestResult()
        {
            var testResult = new TestResult
            {
                GraphOptimizationResults = SetupGraphOptimizationResults(),
            };

            return testResult;
        }

        private static List<GraphOptimizationResult> SetupGraphOptimizationResults()
        {
            var graphOptimizationResults = new List<GraphOptimizationResult>();

            foreach (OptimizationType optimizationType in Enum.GetValues(typeof(OptimizationType)))
            {
                graphOptimizationResults.Add(new GraphOptimizationResult
                {
                    OptimizationType = optimizationType,
                    SearchTypeResults = SetupSearchTypeResults(),
                });
            }
            return graphOptimizationResults;
        }

        private static List<SearchTypeResult> SetupSearchTypeResults()
        {
            var searchTypeResults = new List<SearchTypeResult>();

            foreach (SearchType searchType in Enum.GetValues(typeof(SearchType)))
            {
                searchTypeResults.Add(new SearchTypeResult
                {
                    SearchType = searchType,
                    OpennessResults = new List<OpennessResult>(),
                });
            }

            return searchTypeResults;
        }
    }

    static class Test
    {
        static int searchRepeat = 3;

        /// <summary>
        /// Tests all path-finding algorithms on all graphs and returns the result.
        /// </summary>
        /// <returns>The test result.</returns>
        public static TestResult RunTests()
        {
            Console.WriteLine("Running test...");


            var testResult = ResultSetup.SetupTestResult();

            var optimizationTimer = new Stopwatch();


            // For each graph openness directory
            var opennessDirectories = Directory.GetDirectories(GraphManager.saveLocation);
            int ODIndex = 0;
            foreach (var opennessDirectory in opennessDirectories)
            {
                // print progress
                Console.WriteLine($"{ODIndex + 1}/{opennessDirectories.Length}");

                // parse openness
                double.TryParse(Path.GetFileName(opennessDirectory), out double graphOpenness);

                // For each graph size directory
                var sizeDirectories = SortByGraphSize(Directory.GetDirectories(opennessDirectory));
                foreach (var sizeDirectory in sizeDirectories)
                {
                    // parse size
                    int.TryParse(Path.GetFileName(sizeDirectory), out int graphSize);

                    // For each graph file
                    var graphFiles = Directory.GetFiles(sizeDirectory);
                    foreach (var graphFile in graphFiles)
                    {
                        // parse repeat
                        int.TryParse(Path.GetFileName(graphFile), out int graphRepeat);


                        // For each GraphOptimizationResult
                        foreach (var graphOptimizationResult in testResult.GraphOptimizationResults)
                        {
                            // load graph
                            var graph = GraphManager.Load(graphFile);

                            var sourceNode = 0;
                            var destinationNode = graph.AdjacencyList.Count - 1;

                            // Optimize graph
                            switch (graphOptimizationResult.OptimizationType)
                            {
                                case OptimizationType.None:
                                    optimizationTimer.Restart();
                                    optimizationTimer.Stop();
                                    break;
                                case OptimizationType.Shrinked:
                                    optimizationTimer.Restart();
                                    GraphOptimization.ShrinkGraph(graph, new int[] { sourceNode, destinationNode });
                                    optimizationTimer.Stop();
                                    break;
                                default:
                                    throw new Exception($"{graphOptimizationResult.OptimizationType.ToString()} is missing an optimization function");
                            }

                            var pathfinder = new Pathfinder(graph, sourceNode, destinationNode);

                            // For each SerchTypeResult in current GraphOptimizationResult
                            foreach (SearchTypeResult searchTypeResult in graphOptimizationResult.SearchTypeResults)
                            {
                                // Find the SizeRepeatResult for the current graph in this searchTypeResult
                                var opennessResult = GetOpennessResult(graphOpenness, searchTypeResult);
                                var sizeResult = GetSizeResult(graphSize, opennessResult);
                                var sizeRepeatResult = GetSizeRepeatResult(graphRepeat, sizeResult);

                                // test pathfinder and save results
                                sizeRepeatResult.SearchResults = GetSearchResults(pathfinder, searchTypeResult.SearchType);
                                // set optimization time
                                sizeRepeatResult.GraphOptimizationTime = optimizationTimer.Elapsed.TotalSeconds;
                                
                                // set average result 
                                sizeResult.AverageSearchResult = CalculateAverageSearchResult(sizeResult.SizeRepeatResults, out var agot);
                                sizeResult.AverageGraphOpimizationTime = agot;
                            }
                        }
                    }
                }
                ODIndex++;
            }


            return testResult;
        }

        /// <summary>
        /// Sorts an array of paths to graph size directories by graph size.
        /// </summary>
        /// <param name="graphDirectories">Array to sort.</param>
        /// <returns>Sorted array.</returns>
        private static string[] SortByGraphSize(string[] graphDirectories)
        {
            return graphDirectories.OrderBy(x => ParseInt(x.Split(new string[] { "\\" }, StringSplitOptions.None).Last())).ToArray();
        }

        public static int ParseInt(string s)
        {
            int.TryParse(s, out int result);
            return result;
        }


        /// <summary>
        /// Returns the OpennessResult with a specific openness.
        /// If the SearchTypeResult does not contain an OpennessResult with the matching openness,
        /// a new OpennessResult with that openness will be added and returned.
        /// </summary>
        /// <param name="openness">Openness to match.</param>
        /// <param name="searchTypeResult">SearchTypeResult containing the OpennessResults.</param>
        /// <returns>The OpennessResult with matching openness.</returns>
        private static OpennessResult GetOpennessResult(double openness, SearchTypeResult searchTypeResult)
        {
            if (searchTypeResult.OpennessResults == null)
                searchTypeResult.OpennessResults = new List<OpennessResult>();
            

            int index = searchTypeResult.OpennessResults.FindIndex(x => x.Openness == openness);

            if (index == -1)
            {
                searchTypeResult.OpennessResults.Add(new OpennessResult{ Openness = openness });
                return searchTypeResult.OpennessResults.Last();
            }
            else
                return searchTypeResult.OpennessResults[index];
        }

        /// <summary>
        /// Returns the SizeResult with s specific size.
        /// If the OpennessResult does not contain a SizeResult with the matching size,
        /// a new SizeReult with that size will be added and returned.
        /// </summary>
        /// <param name="size">Size to match.</param>
        /// <param name="opennessResult">OpennessResult containing the SizeResults.</param>
        /// <returns>The SizeResult with matching size.</returns>
        private static SizeResult GetSizeResult(int size, OpennessResult opennessResult)
        {
            if (opennessResult.SizeResults == null)
                opennessResult.SizeResults = new List<SizeResult>();

            int index = opennessResult.SizeResults.FindIndex(x => x.GraphSize == size);

            if (index == -1)
            {
                opennessResult.SizeResults.Add(new SizeResult
                {
                    GraphSize = size,
                });
                return opennessResult.SizeResults.Last();
            }
            else
                return opennessResult.SizeResults[index];
        }

        /// <summary>
        /// Returns the SizeRepeatResult with a specific openness.
        /// If the SizeResult does no contain a SizeRepeatResult with the matching repeat,
        /// a new SizeRepeatResult will be added and returned.
        /// </summary>
        /// <param name="repeat">Repeat to match.</param>
        /// <param name="sizeResult">SizeResult containing the SizeRepeatResults</param>
        /// <returns>The SizeResult with matching size.</returns>
        private static SizeRepeatResult GetSizeRepeatResult(int repeat, SizeResult sizeResult)
        {
            if (sizeResult.SizeRepeatResults == null)
                sizeResult.SizeRepeatResults = new List<SizeRepeatResult>();

            int index = sizeResult.SizeRepeatResults.FindIndex(x => x.GraphSizeRepet == repeat);

            if (index == -1)
            {
                sizeResult.SizeRepeatResults.Add(new SizeRepeatResult
                {
                    GraphSizeRepet = repeat,
                    SearchResults = new List<SearchResult>(),
                });
                return sizeResult.SizeRepeatResults.Last();
            }
            else
                return sizeResult.SizeRepeatResults[index];
        }

        /// <summary>
        /// Returns the AverageSearchResult from a list of SizeRepeatResults. out AverageGraphOpimizationTime.
        /// </summary>
        /// <param name="sizeRepeatResults">SizeRepeatResults to calculate the AverageSearchResult from.</param>
        /// <param name="averageGraphOpimizationTime">The AverageGraphOpimizationTime.</param>
        /// <returns>The AverageSearchResult.</returns>
        private static AverageSearchResult CalculateAverageSearchResult(List<SizeRepeatResult> sizeRepeatResults, out AverageGraphOpimizationTime averageGraphOpimizationTime)
        {
            // the sum of all search results
            var searchResSum = new SearchResult
            {
                SearchTime = 0,
                ExplordedNodes = 0,
                ExploredRatio = 0,
            };
            double graphOptimizationTimeSum = 0;

            var allSearchTimes = new List<double>();
            var allExploredNodes = new List<int>();
            var allExploredRatios = new List<double>();
            var allGraphOpimizationTimes = new List<double>();

            // the total amount of search results
            var searchResCount = 0;
            
            // Set searchResSum and searchResCount
            foreach (var sizeRepeatResult in sizeRepeatResults)
            {
                allGraphOpimizationTimes.Add(sizeRepeatResult.GraphOptimizationTime);
                graphOptimizationTimeSum += sizeRepeatResult.GraphOptimizationTime;

                foreach (var searchResult in sizeRepeatResult.SearchResults)
                {
                    searchResSum.SearchTime += searchResult.SearchTime;
                    searchResSum.ExplordedNodes += searchResult.ExplordedNodes;
                    searchResSum.ExploredRatio += searchResult.ExploredRatio;

                    AddSorted(allSearchTimes, searchResult.SearchTime);
                    AddSorted(allExploredNodes, searchResult.ExplordedNodes);
                    AddSorted(allExploredRatios, searchResult.ExploredRatio);
                }
                searchResCount += sizeRepeatResult.SearchResults.Count;
            }

            averageGraphOpimizationTime = new AverageGraphOpimizationTime
            {
                MeanOptimizationTime = graphOptimizationTimeSum / sizeRepeatResults.Count,
                MedianOpimizatonTime = GetCenterValue(allGraphOpimizationTimes),
            };

            return new AverageSearchResult
            {
                MeanSearchResult = new SearchResult
                {
                    SearchTime = searchResSum.SearchTime / searchResCount,
                    ExplordedNodes = searchResSum.ExplordedNodes / searchResCount,
                    ExploredRatio = searchResSum.ExploredRatio / searchResCount,
                },
                MedianSearchResult = new SearchResult
                {
                    SearchTime = GetCenterValue(allSearchTimes),
                    ExplordedNodes = GetCenterValue(allExploredNodes),
                    ExploredRatio = GetCenterValue(allExploredRatios),
                },
            };
        }

        /// <summary>
        /// Inserts a value at its sorted position.
        /// </summary>
        /// <param name="list">List to insert value in.</param>
        /// <param name="value">Value to insert.</param>
        public static void AddSorted<T>(this List<T> list, T value)
        {
            int x = list.BinarySearch(value);
            list.Insert((x >= 0) ? x : ~x, value);
        }

        /// <summary>
        /// Returns the value in the center of the list.
        /// If the list has an uneven length, the average between the two values in the center will be returned.
        /// </summary>
        /// <param name="list">List with values.</param>
        /// <returns>The center value in the list.</returns>
        public static T GetCenterValue<T>(List<T> list)
        {
            if (list == null || list.Count == 0)
                return default;
            if (list.Count == 1)
                return list[0];
            else
                return list.Count % 2 == 0
                    ? (((dynamic)list[list.Count / 2 - 1] + list[list.Count / 2])/2)
                    : list[list.Count / 2];
        }


        /// <summary>
        /// Tests a graph with the given searchType multiple times and returns the results.
        /// </summary>
        /// <returns></returns>
        private static List<SearchResult> GetSearchResults(Pathfinder pathfinder, SearchType searchType)
        {
            var searchResults = new List<SearchResult>();

            for (int i = 0; i < searchRepeat; i++)
            {
                searchResults.Add(GetSearchResult(pathfinder, searchType));
            }

            return searchResults;
        }

        /// <summary>
        /// Tests a graph with the given searchType and returns the result.
        /// </summary>
        /// <param name="pathfinder"></param>
        /// <param name="searchType"></param>
        /// <returns></returns>
        private static SearchResult GetSearchResult(Pathfinder pathfinder, SearchType searchType)
        {
            var stopwatch = new Stopwatch(); // TODO: don't create new stopwatch each time

            // start timer
            stopwatch.Restart();
            // run pathfinder
            bool foundPath = pathfinder.FindPath(searchType, out var path);
            // stop timer
            stopwatch.Stop();

            // if the path was not found, time will be set to -1
            var elapsedTime = foundPath ? stopwatch.Elapsed.TotalSeconds : -1;

            return new SearchResult
            {
                SearchTime = elapsedTime,
            };
        }
    }
}
