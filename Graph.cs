using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallFlowVisualizer
{

    /// <summary>
    /// Node Graph
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class Graph<T>
    {
        internal Graph() { }
        internal Graph(List<JToken> vertices, List<Tuple<string, string>> edges)
        {
            foreach (var vertex_i in vertices)
                AddVertex((string)vertex_i["id"]);

            foreach (var edge_i in edges)
                AddEdge(edge_i);
        }

        internal Dictionary<string, HashSet<string>> AdjacencyList { get; } = new Dictionary<string, HashSet<string>>();

        internal void AddVertex(string vertex)
        {
            AdjacencyList[vertex] = new HashSet<string>();
        }

        internal void AddEdge(Tuple<string, string> edge)
        {
            if (AdjacencyList.ContainsKey(edge.Item1) && AdjacencyList.ContainsKey(edge.Item2))
            {
                AdjacencyList[edge.Item1].Add(edge.Item2);

            }
        }

    }

    
}
