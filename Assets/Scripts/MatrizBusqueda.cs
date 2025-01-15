using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MatrizBusqueda : MonoBehaviour
{
    public GameObject nodePrefab;       // Prefab para cada nodo (Button)
    public Transform gridParent;        // Contenedor del grid (Panel con GridLayoutGroup)
    public int gridWidth = 5;           // Ancho inicial del mapa
    public int gridHeight = 5;          // Altura inicial del mapa

    public GameObject actorPrefab;      // Prefab del actor
    public GameObject goalPrefab;       // Prefab de la meta

    private GameObject actor;
    private GameObject goal;

    private Node startNode, goalNode;
    private List<Node> path;
    private List<Node> grid = new List<Node>();

    public Color pathColor = Color.blue;
    public Color startColor = Color.green;
    public Color goalColor = Color.red;

    private bool placingActor = true;
    private bool isGameActive = true;  // Controla si el juego está activo o no
    private bool isMoving = false;     // Controla si el actor está en movimiento
    private bool isCompleted = false;  // Indica si el recorrido ha terminado

    public Button restartButton;       // Botón de reinicio

    public class Node
    {
        public Vector2 position;
        public Button button;
        public List<Node> neighbors = new List<Node>();

        // Nuevos campos para A*
        public float gCost;
        public float hCost;
        public Node parent;

        public Node(Vector2 position, Button button)
        {
            this.position = position;
            this.button = button;
        }
    }

    void Start()
    {
        GenerateGrid(gridWidth, gridHeight);
        restartButton.onClick.AddListener(RestartGame);  // Asigna la acción de reinicio al botón
    }

    void GenerateGrid(int width, int height)
    {
        // Limpia el grid existente
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }
        grid.Clear();

        // Obtén el tamaño del Canvas
        RectTransform canvasRect = gridParent.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        // Define el espacio entre los nodos
        float spacingX = 10f;  // Espacio horizontal entre nodos
        float spacingY = 10f;  // Espacio vertical entre nodos

        // Calcula el tamaño de cada celda, incluyendo el espacio entre nodos
        float totalWidth = canvasWidth - (spacingX * (width - 1));  // Restar el espacio entre nodos
        float totalHeight = canvasHeight - (spacingY * (height - 1));  // Restar el espacio entre nodos

        // Calcula el tamaño de cada nodo, ajustado al espacio disponible
        float nodeWidth = totalWidth / width;
        float nodeHeight = totalHeight / height;

        // Determina el tamaño del nodo (mínimo entre el ancho y el alto calculado)
        float nodeSize = Mathf.Min(nodeWidth, nodeHeight);

        // Ajustar las configuraciones del GridLayoutGroup para que distribuya los nodos en una cuadrícula
        GridLayoutGroup gridLayout = gridParent.GetComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(nodeSize, nodeSize); // Configura el tamaño de cada celda del GridLayoutGroup
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = width; // Establece el número de columnas que se deben mostrar

        // Configura el espacio entre los nodos
        gridLayout.spacing = new Vector2(spacingX, spacingY); // Espacio entre los nodos (ajusta este valor para más espacio)

        // Asegúrate de que las opciones de expansión no cambien el tamaño de las celdas
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.padding = new RectOffset(10, 10, 10, 10); // Puedes ajustar el padding si es necesario

        // Genera el nuevo grid
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 position = new Vector2(x, y);

                // Instancia el botón
                GameObject nodeObject = Instantiate(nodePrefab, gridParent);
                Button nodeButton = nodeObject.GetComponent<Button>();
                nodeObject.GetComponent<Image>().color = Color.white;

                // Configura el nodo
                Node node = new Node(position, nodeButton);
                grid.Add(node);

                // Configura el evento de clic, solo si el juego está activo y no está completado
                int index = grid.Count - 1;
                nodeButton.onClick.AddListener(() => HandleClick(grid[index]));
            }
        }

        ConnectNeighbors();
    }

    void ConnectNeighbors()
    {
        foreach (var currentNode in grid)
        {
            foreach (var potentialNeighbor in grid)
            {
                if (Vector2.Distance(currentNode.position, potentialNeighbor.position) <= 1.1f)
                {
                    currentNode.neighbors.Add(potentialNeighbor);
                }
            }
        }
    }

    void HandleClick(Node clickedNode)
    {
        // Solo permite seleccionar nodos si el juego está activo, el actor no se está moviendo y no se ha completado el recorrido
        if (!isGameActive || isMoving || isCompleted) return;

        if (placingActor)
        {
            startNode = clickedNode;
            clickedNode.button.GetComponent<Image>().color = startColor;
            actor = Instantiate(actorPrefab, clickedNode.button.transform.position, Quaternion.identity, gridParent);
            placingActor = false;
        }
        else
        {
            goalNode = clickedNode;
            clickedNode.button.GetComponent<Image>().color = goalColor;
            goal = Instantiate(goalPrefab, clickedNode.button.transform.position, Quaternion.identity, gridParent);

            FindAndMoveToGoal();
        }
    }

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

    List<Node> FindPath(Node startNode, Node goalNode)
    {
        List<Node> openList = new List<Node>();
        List<Node> closedList = new List<Node>();

        foreach (var node in grid)
        {
            node.gCost = float.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }

        startNode.gCost = 0;
        startNode.hCost = GetHeuristic(startNode, goalNode);
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            Node currentNode = GetNodeWithLowestFCost(openList);

            if (currentNode == goalNode)
            {
                return ReconstructPath(goalNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (var neighbor in currentNode.neighbors)
            {
                if (closedList.Contains(neighbor)) continue;

                float tentativeGCost = currentNode.gCost + 1;

                if (!openList.Contains(neighbor) || tentativeGCost < neighbor.gCost)
                {
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = GetHeuristic(neighbor, goalNode);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

    float GetHeuristic(Node node, Node goalNode)
    {
        return Mathf.Abs(node.position.x - goalNode.position.x) + Mathf.Abs(node.position.y - goalNode.position.y);
    }

    Node GetNodeWithLowestFCost(List<Node> openList)
    {
        Node lowestFCostNode = openList[0];
        foreach (var node in openList)
        {
            if (node.gCost + node.hCost < lowestFCostNode.gCost + lowestFCostNode.hCost)
            {
                lowestFCostNode = node;
            }
        }
        return lowestFCostNode;
    }

    List<Node> ReconstructPath(Node goalNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = goalNode;

        while (currentNode != null)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    IEnumerator MoveActorAlongPath()
    {
        isMoving = true;  // Activar la bandera de movimiento

        foreach (var node in path)
        {
            if (node.button != null)
            {
                node.button.GetComponent<Image>().color = pathColor;
            }

            if (actor != null)
            {
                Vector3 worldPosition = node.button.transform.position;
                actor.transform.position = worldPosition;
            }

            yield return new WaitForSeconds(0.5f);
        }

        isMoving = false;  // Desactivar la bandera de movimiento
        isCompleted = true; // Marcar como completado
        Debug.Log("El actor llegó a la meta.");
    }

    // Método para reiniciar el juego
    void RestartGame()
    {
        isGameActive = true;  // Habilitar la interacción
        placingActor = true;  // Permitir colocar un nuevo actor
        isMoving = false;     // Asegurarse de que no estamos en movimiento
        isCompleted = false;  // Reiniciar el estado de completado
        grid.Clear();  // Limpiar la cuadrícula
        actor = null;  // Eliminar al actor
        goal = null;   // Eliminar la meta
        GenerateGrid(gridWidth, gridHeight);  // Regenerar el mapa
    }
}





