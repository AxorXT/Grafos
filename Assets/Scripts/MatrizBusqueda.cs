using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatrizBusqueda : MonoBehaviour
{
    public GameObject actorPrefab;  // Prefab del actor
    public GameObject goalPrefab;   // Prefab de la meta
    private GameObject actor;       // Instancia del actor
    private GameObject goal;        // Instancia de la meta

    public List<Node> grid = new List<Node>();  // Lista de nodos

    private Node startNode, goalNode;  // Nodo de inicio y nodo de meta
    private List<Node> path;           // Camino más corto

    private Material originalMaterial;  // Material original de las casillas
    public Material pathMaterial;       // Material para resaltar el camino
    public Material InitialgoalMaterial;  // Material para la meta
    public Material goalReachedMaterial; // Material para la meta

    // Clase de Nodo que representa cada casilla
    public class Node
    {
        public Vector2 position;
        public GameObject visualNode;  // Representación visual del nodo
        public List<Node> neighbors = new List<Node>();

        public Node(Vector2 position, GameObject visualNode)
        {
            this.position = position;
            this.visualNode = visualNode;
        }
    }

    void Start()
    {
        GenerateGraphFromScene();   // Generar grafo desde las casillas
        PlaceActorAndGoal();        // Colocar actor y meta aleatoriamente
        FindAndMoveToGoal();        // Calcular el camino y mover el actor
    }

    // Generar el grafo desde las casillas etiquetadas como "Walkable"
    void GenerateGraphFromScene()
    {
        var walkableObjects = GameObject.FindGameObjectsWithTag("Walkable");

        foreach (var obj in walkableObjects)
        {
            // Crear un nodo para cada casilla
            Node node = new Node(obj.transform.position, obj); // Asociamos un nodo con cada GameObject
            grid.Add(node); // Agregar el nodo a la lista

            originalMaterial = obj.GetComponent<Renderer>().material; // Guardar el material original
        }

        // Conectar nodos vecinos (solo ortogonales)
        foreach (var currentNode in grid)  // Renombramos 'node' a 'currentNode'
        {
            float tolerance = 0.1f;

            foreach (var potentialNeighbor in grid)
            {
                // Asegurarse de que la distancia en X o Y sea aproximadamente 1 unidad
                if ((Mathf.Abs(currentNode.position.x - potentialNeighbor.position.x) <= 300 + tolerance && currentNode.position.y == potentialNeighbor.position.y) ||
                    (Mathf.Abs(currentNode.position.y - potentialNeighbor.position.y) <= 300 + tolerance && currentNode.position.x == potentialNeighbor.position.x))
                {
                    currentNode.neighbors.Add(potentialNeighbor);  // Agregar vecinos ortogonales al nodo
                }
            }
        }
    }

    // Colocar actor y meta aleatoriamente
    void PlaceActorAndGoal()
    {
        // Seleccionamos nodos aleatorios para el inicio y la meta
        startNode = grid[Random.Range(0, grid.Count)];
        do
        {
            goalNode = grid[Random.Range(0, grid.Count)];
        } while (goalNode == startNode);  // Asegurarse de que la meta no sea el mismo nodo que el inicio

        // Instanciamos el actor y la meta en los nodos seleccionados
        actor = Instantiate(actorPrefab, startNode.position, Quaternion.identity);
        goal = Instantiate(goalPrefab, goalNode.position, Quaternion.identity);

        // Cambiar el material de la meta al material que desees (por ejemplo, un material que tenga un color diferente)
        if (goal != null)
        {
            Renderer goalRenderer = goal.GetComponent<Renderer>(); // Obtener el componente Renderer de la meta
            if (goalRenderer != null)  // Verificar si el componente Renderer existe
            {
                goalRenderer.material = InitialgoalMaterial;  // Cambiar el material de la meta
            }
            else
            {
                Debug.LogError("El objeto de la meta no tiene un componente Renderer.");
            }
        }
    }

    // Calcular el camino más corto y mover al actor
    void FindAndMoveToGoal()
    {
        path = FindPath(startNode, goalNode);
        if (path == null || path.Count == 0)
        {
            Debug.LogError("No se encontró un camino.");
            return;
        }

        StartCoroutine(MoveActorAlongPath());
    }

    // Algoritmo A* para calcular el camino más corto
    List<Node> FindPath(Node startNode, Node goalNode)
    {
        var openSet = new List<Node> { startNode };
        var cameFrom = new Dictionary<Node, Node>();
        var gScore = new Dictionary<Node, float>();
        var fScore = new Dictionary<Node, float>();

        foreach (var node in grid)
        {
            gScore[node] = float.MaxValue;
            fScore[node] = float.MaxValue;
        }

        gScore[startNode] = 0;
        fScore[startNode] = Heuristic(startNode, goalNode);

        while (openSet.Count > 0)
        {
            Node current = openSet[0];
            foreach (var node in openSet)
            {
                if (fScore[node] < fScore[current])
                    current = node;
            }

            if (current == goalNode)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            foreach (var neighbor in current.neighbors)
            {
                float tentativeGScore = gScore[current] + 1;

                if (tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goalNode);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // No se encontró un camino
    }

    // Heurística de A* (distancia Manhattan)
    float Heuristic(Node a, Node b)
    {
        return Mathf.Abs(a.position.x - b.position.x) + Mathf.Abs(a.position.y - b.position.y);
    }

    // Reconstruir el camino desde el nodo final al nodo inicial
    List<Node> ReconstructPath(Dictionary<Node, Node> cameFrom, Node current)
    {
        var totalPath = new List<Node> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Add(current);
        }
        totalPath.Reverse();
        return totalPath;
    }

    // Mover el actor por el camino
    IEnumerator MoveActorAlongPath()
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogError("No se encontró un camino para mover al actor.");
            yield break;  // Detener la ejecución si no hay camino
        }

        foreach (var node in path)
        {
            if (node.visualNode == null)
            {
                Debug.LogError("El nodo no tiene asignado un objeto visual.");
                yield break;  // Detener la ejecución si no hay visualNode en el nodo
            }

            // Cambiar color del nodo actual para mostrar el camino
            node.visualNode.GetComponent<Renderer>().material = pathMaterial;

            if (actor != null)
            {
                actor.transform.position = node.position;
            }
            else
            {
                Debug.LogError("El actor no está instanciado.");
                yield break;  // Detener la ejecución si el actor es null
            }

            yield return new WaitForSeconds(0.5f);
        }

        // Cambiar el color de la meta cuando el actor llegue
        if (goal != null)
        {
            Renderer goalRenderer = goal.GetComponent<Renderer>();
            if (goalRenderer != null)
            {
                goalRenderer.material = goalReachedMaterial;  // Cambiar el color de la meta cuando el actor llegue
            }
        }

        Debug.Log("El actor llegó a la meta.");
    }
}