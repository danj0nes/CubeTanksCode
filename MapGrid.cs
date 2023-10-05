
/*
    This class is for random level generation. From the given width and height, it will randomly add all the walls, 
    checking all the conditions that need to be met. Then it will add the player and the enemies to the grid.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;
using System.Linq;
using System;

public class MapGrid
{
    private readonly MapGridSquare[][] grid;
    private readonly float squareSize = 8f;
    private readonly int width = 10;
    private readonly int height = 5;
    private readonly int maxWallNeighbours = 8;
    private readonly int maxChunks = 5;

    public float mapRating;

    private const float MIN_DISTANCE_FROM_PLAYER = 7f;
    private const float MIN_DISTANCE_FROM_TANK = 2.5f;

    private List<Vector2> wallCoordList;
    private List<Vector2> pathCoordList;
    private List<Vector2> tankCoordList;
    private List<Vector2> validLocations;
    private List<Vector2> validTankLocations;

    private int currentChunks = 0;

    //RunTime Variables
    private Vector2 playerRunTimeCoord;
    private List<Vector2> obstacleCoordList;
    private List<TankContainer> tankList;

    private delegate bool CheckMethod(MapGridSquare newSquare);


    //------------------------CONSTRUCTORS-----------------------------

    // SURVIVAL
    public MapGrid(float squareSize, int width, int height, int desiredWalls, int[] desiredTankArray, int maxWallNeighbours, int maxChunks) 
    {
        this.width = width;
        this.height = height;
        this.squareSize = squareSize;
        this.maxWallNeighbours = maxWallNeighbours;
        this.maxChunks = maxChunks;

        grid = new MapGridSquare[width][];

        for (int i = 0; i < width; i++)
        {
            grid[i] = new MapGridSquare[height];
        }

        EmptyGrid();

        //WALLS
        GenerateWalls(desiredWalls);
        Display();

        //TANKS
        GenerateTanks(desiredTankArray.Length);
        Display();

        tankList = new();
        obstacleCoordList = new();

        for (int i = 0; i < tankCoordList.Count; i++)
        {
            TankContainer tank = new((TankType)desiredTankArray[i], ToGamePosition(tankCoordList[i], squareSize / 3f), Vector2ToIndex(tankCoordList[i]));

            if (tank.object_tag == "Static")
            {
                obstacleCoordList.Add(tankCoordList[i]);
            }
            tankList.Add(tank);
        }

        CalculateMapRating();

    }
    // RATED
    public MapGrid(int levelRating, float squareSize, int width, int height, int desiredWalls, int desiredTanks, int maxWallNeighbours, int maxChunks)
    {
        this.width = width;
        this.height = height;
        this.squareSize = squareSize;
        this.maxWallNeighbours = maxWallNeighbours;
        this.maxChunks = maxChunks;


        grid = new MapGridSquare[width][];

        for (int i = 0; i < width; i++)
        {
            grid[i] = new MapGridSquare[height];
        }


        //GRID 
        EmptyGrid();

        //WALLS
        GenerateWalls(desiredWalls);
        Display();

        //TANKS
        GenerateTanks(desiredTanks);
        Display();

        obstacleCoordList = new();
        SetTankType(levelRating);

        CalculateMapRating();

    }
    public MapGrid(CompressedMapGrid compressedMap) 
    {
        width = compressedMap.width;
        height = compressedMap.height;

        grid = new MapGridSquare[width][];

        for (int i = 0; i < width; i++)
        {
            grid[i] = new MapGridSquare[height];
        }

        EmptyGrid();

        wallCoordList = new();
        validLocations = new(pathCoordList);

        foreach (int index in compressedMap.wallArray) 
        {
            MapGridSquare newWall = GetSquare(IndexToVector2(index));
            AddWall(newWall);
        }

        Display();

        tankCoordList = new();
        validTankLocations = new(pathCoordList);

        foreach (int index in compressedMap.tankArray)
        {
            MapGridSquare newTankSquare = GetSquare(IndexToVector2(index));
            AddTank(newTankSquare);
        }

        Display();

        tankList = new();
        obstacleCoordList = new();

        for (int i = 0; i < tankCoordList.Count; i++)
        {
            TankContainer tank = new((TankType)compressedMap.tankTypeArray[i], ToGamePosition(tankCoordList[i], squareSize / 3f), Vector2ToIndex(tankCoordList[i]));

            if (tank.object_tag == "Static")
            {
                obstacleCoordList.Add(tankCoordList[i]);
            }
            tankList.Add(tank);
        }

        CalculateMapRating();
    }

    //-----------------------------------------------------------------

    public int Width { get { return width; } }
    public int Height { get { return height; } }
    public List<Vector2> WallCoordList { get { return wallCoordList; } }
    public List<TankContainer> TankList { get { return tankList; } }
    public MapGridSquare GetSquare(Vector2 coord) { return grid[(int)coord.x][(int)coord.y]; }

    void EmptyGrid()
    {
        pathCoordList = new();

        //start from top left going down
        for (int i_height = 0; i_height < height; i_height++)
        {
            for (int i_width = 0; i_width < width; i_width++)
            {
                Vector2 mapSquareCoord = new(i_width, i_height);
                pathCoordList.Add(mapSquareCoord);
                grid[i_width][i_height] = new MapGridSquare(Type.path, mapSquareCoord, height, width);
            }
        }
        validTankLocations = new(pathCoordList);
    }
    void GenerateWalls(int desiredWalls)
    {
        void GenerateWall(List<Vector2> locationsToTry)
        {
            Vector2 newWallCoord = locationsToTry[UnityEngine.Random.Range(0, locationsToTry.Count)];
            MapGridSquare newWall = GetSquare(newWallCoord);

            AddWall(newWall);

            if (!PassedWallDuringChecks(newWall))
            {
                locationsToTry.Remove(newWallCoord);

                if (locationsToTry.Count != 0)
                {
                    GenerateWall(locationsToTry);
                }
                return;

            }

            ValidateLocations(newWall);
        }

        wallCoordList = new();
        validLocations = new(pathCoordList);

        while (wallCoordList.Count < desiredWalls && validLocations.Count != 0)
        {
            GenerateWall(new List<Vector2>(validLocations));
        }

        if (!PassedWallEndChecks())
        {
            ResetWalls();
            GenerateWalls(desiredWalls);
        }
    }
    void ResetWalls()
    {
        foreach (Vector2 path in pathCoordList)
        {
            grid[(int)path.x][(int)path.y].failedcheck = FailedCheck.none;
        }
        foreach (Vector2 wall in wallCoordList)
        {
            RemoveWall(grid[(int)wall.x][(int)wall.y]);
        }
        pathCoordList = new();
        validTankLocations = new();

        for (int i_height = 0; i_height < height; i_height++)
        {
            for (int i_width = 0; i_width < width; i_width++)
            {
                Vector2 mapSquareCoord = new(i_width, i_height);
                pathCoordList.Add(mapSquareCoord);
            }
        }

        validTankLocations = pathCoordList;
    }
    void GenerateTanks(int desiredTanks)
    {
        void GenerateTank(List<Vector2> locationsToTry)
        {
            Vector2 newTankSquareCoord = locationsToTry[UnityEngine.Random.Range(0, locationsToTry.Count)];
            MapGridSquare newTankSquare = GetSquare(newTankSquareCoord);

            AddTank(newTankSquare);

            if (!PassedTankDuringChecks(newTankSquare))
            {
                locationsToTry.Remove(newTankSquareCoord);


                if (locationsToTry.Count != 0)
                {
                    GenerateTank(locationsToTry);
                }
                return;

            }
        }
        tankCoordList = new();

        validTankLocations = new(pathCoordList);

        while (tankCoordList.Count < desiredTanks && validTankLocations.Count != 0)
        {
            GenerateTank(new List<Vector2>(validTankLocations));
        }

        if (!PassedTankEndChecks())
        {
            GenerateTanks(desiredTanks);
        }
    }
    void SetTankType(int levelRating)
    {
        TankType PickRandomTank()
        {
            float sum = 0f;
            foreach (TankType value in Enum.GetValues(typeof(TankType)))
            {
                sum += value.GetProbability(levelRating);
            }
            float randomnumber = UnityEngine.Random.Range(0f, sum);

            foreach (TankType value in Enum.GetValues(typeof(TankType)))
            {
                randomnumber -= value.GetProbability(levelRating);
                if (randomnumber <= 0f)
                {
                    return value;
                }
            }

            throw new Exception("ERROR PICKING RANDOM TANKTYPE");
        }

        tankList = new();

        for (int i = 0; i < tankCoordList.Count; i++)
        {
            TankContainer tank;
            if (i == 0)
            {
                tank = new TankContainer(TankType.PlayerTank, ToGamePosition(tankCoordList[i], squareSize / 3f), Vector2ToIndex(tankCoordList[i]));
            }
            else
            {
                tank = new TankContainer(PickRandomTank(), ToGamePosition(tankCoordList[i], squareSize / 3f), Vector2ToIndex(tankCoordList[i]));
                if (tank.object_tag == "Static")
                {
                    obstacleCoordList.Add(tankCoordList[i]);
                }
            }
            tankList.Add(tank);
        }
    }

    private void CalculateMapRating() 
    {
        // type of tanks
        // amount of enemies

        float ratingsum = 0;
        foreach (TankContainer tankValue in tankList) 
        {
            ratingsum += tankValue.tankType.GetFirstAppearanceRating();
        }
        ratingsum /= (tankList.Count - 1); // minus 1 because of player tank
        mapRating = ratingsum;
    }

    private void AddWall(MapGridSquare square)
    {
        Vector2 square_coord = square.coord;
        wallCoordList.Add(square_coord);
        pathCoordList.Remove(square_coord);
        square.type = Type.wall;
        foreach (Vector2 number in square.neighbour_coords)
        {
            grid[(int)number.x][(int)number.y].wall_neighbours.Add(square_coord);
            grid[(int)number.x][(int)number.y].path_neighbours.Remove(square_coord);
        }
        if (square.wall_neighbours.Count == 0) { currentChunks++; }

        validLocations.Remove(square_coord);
    }
    private void AddTank(MapGridSquare square)
    {
        tankCoordList.Add(square.coord);
        if (tankCoordList.Count == 1)
        {
            square.type = Type.player;
            playerRunTimeCoord = square.coord;
        }
        else { square.type = Type.tank; }
        
        validTankLocations.Remove(square.coord);
    }
    private void RemoveWall(MapGridSquare square)
    {
        Vector2 square_coord = square.coord;
        wallCoordList.Remove(square_coord);
        pathCoordList.Add(square_coord);
        square.type = Type.path;
        foreach (Vector2 number in square.neighbour_coords)
        {
            grid[(int)number.x][(int)number.y].wall_neighbours.Remove(square_coord);
            grid[(int)number.x][(int)number.y].path_neighbours.Add(square_coord);
        }
        if (square.wall_neighbours.Count == 0) { currentChunks--; }

        validLocations.Add(square_coord);
    }
    private void RemoveTank(MapGridSquare square)
    {
        tankCoordList.Remove(square.coord);
        square.type = Type.path;
          
        validTankLocations.Add(square.coord);
    }

    private bool PassedWallDuringChecks(MapGridSquare newWall)
    {

        CheckMethod[] CheckMethodArray = new CheckMethod[] {
                ChunkDistanceCheck,
                ChunkBoundaryDistance,
                MaxChunkCheck,
                DiagonalToWallCheck,
                CShapeCheck,
                PathConnectivityCheck,
                MaxNeighbourCheck,
                LargeCShapeCheck,
                DiagonalGapCheck
            };

        for (int enum_value = 0; enum_value < CheckMethodArray.Length; enum_value++)
        {
            if (!CheckMethodArray[enum_value](newWall))
            {
                RemoveWall(newWall);
                validLocations.Remove(newWall.coord);
                newWall.failedcheck = (FailedCheck)enum_value;
                return false;
            }
        }

        return true;

    }
    private bool PassedTankDuringChecks(MapGridSquare newTankSquare)
    {
        CheckMethod[] CheckMethodArray = new CheckMethod[] {
                TankProxCheck,
                PlayerProxCheck,
            };

        for (int enum_value = 0; enum_value < CheckMethodArray.Length; enum_value++)
        {
            if (!CheckMethodArray[enum_value](newTankSquare))
            {
                RemoveTank(newTankSquare);
                validTankLocations.Remove(newTankSquare.coord);
                return false;
            }
        }

        /*
         * distance from first tank placed
         * 
         * distance between tanks
         */
        return true;
    }

    private void ValidateLocations(MapGridSquare square)
    {
        Vector2[] listCopy = square.path_neighbours.ToArray();
        foreach (Vector2 location in listCopy)
        {
            MapGridSquare invalidSquare = GetSquare(location);
            FailedCheck check = invalidSquare.failedcheck;
            if (check != FailedCheck.none)
            {
                AddWall(invalidSquare);

                if (PassedWallDuringChecks(invalidSquare)) 
                {
                    invalidSquare.failedcheck = FailedCheck.none;
                    RemoveWall(invalidSquare);
                }
            }
        }
    }

    private bool PassedWallEndChecks()
    {
        return true;
    }
    private bool PassedTankEndChecks()
    {
        return true;
    }



    // Start of Wall During Checks
    private bool MaxNeighbourCheck(MapGridSquare newWall)
    {
        return !(newWall.neighbour_coords.Where(coord => GetSquare(coord).HasMaxedWallNeighbours(maxWallNeighbours)).Any());
    }
    private bool PathConnectivityCheck(MapGridSquare newWall)
    {
        void PathConnective(List<Vector2> pathIndexListCopy, MapGridSquare pathSquare)
        {
            pathIndexListCopy.Remove(pathSquare.coord);
            foreach (Vector2 neighbour_coord in pathSquare.orthagonal_neighbour_coords)
            {
                if (pathIndexListCopy.Contains(neighbour_coord))
                {
                    PathConnective(pathIndexListCopy, grid[(int)neighbour_coord.x][(int)neighbour_coord.y]);
                }
            }
        }
        List<Vector2> pathCoordListCopy = new(pathCoordList);

        PathConnective(pathCoordListCopy, grid[(int)pathCoordListCopy[0].x][(int)pathCoordListCopy[0].y]);


        return pathCoordListCopy.Count == 0;
    }
    private bool DiagonalToWallCheck(MapGridSquare newWall)
    {

        bool CommonOrthagonalWallNeighbour(MapGridSquare wall1, MapGridSquare wall2)
        {
            Vector2[] commonElementsArray = wall1.orthagonal_neighbour_coords.Intersect(wall2.orthagonal_neighbour_coords).ToArray();
            foreach (Vector2 commonElement in commonElementsArray)
            {
                if (GetSquare(commonElement).type == Type.wall) { return true; }
            }
            return false;
        }

        foreach (Vector2 neighbour in newWall.diagonal_neighbour_coords.Intersect(newWall.wall_neighbours))
        {
            if (!CommonOrthagonalWallNeighbour(newWall, GetSquare(neighbour))) { return false; }
        }

        return true;
    }
    private bool ChunkDistanceCheck(MapGridSquare newWall)
    {
        List<Vector2> ChunkIndexList(List<Vector2> chunkIndexList, MapGridSquare wall)
        {
            chunkIndexList.Add(wall.coord);
            foreach (Vector2 wallNeighbour in wall.wall_neighbours)
            {
                if (!chunkIndexList.Contains(wallNeighbour))
                {
                    ChunkIndexList(chunkIndexList, GetSquare(wallNeighbour));
                }
            }
            return chunkIndexList;
        }

        List<Vector2> chunkIndexList = ChunkIndexList(new List<Vector2>(), newWall);

        foreach (Vector2 neighbour in newWall.path_neighbours)
        {
            if (GetSquare(neighbour).wall_neighbours.Except(chunkIndexList).Count() > 0)
            {
                return false;
            }
        }

        return true;
    }
    private bool ChunkBoundaryDistance(MapGridSquare newWall)
    {
        foreach (Vector2 neighbour in newWall.orthagonal_neighbour_coords)
        {
            MapGridSquare square = grid[(int)neighbour.x][(int)neighbour.y];
            if (square.type == Type.path)
            {
                if (square.IsEdge() && !(newWall.IsEdge()))
                {
                    return false;
                }
                if (square.IsCorner())
                {
                    return false;
                }
            }
        }
        return true;
    }
    private bool MaxChunkCheck(MapGridSquare newWall)
    {
        if (newWall.wall_neighbours.Count == 0)
        {
            if (currentChunks > maxChunks)
            {
                return false;
            }
        }
        return true;
    }
    private bool CShapeCheck(MapGridSquare newWall)
    {
        foreach (Vector2 neighbour in newWall.path_neighbours.Intersect(newWall.orthagonal_neighbour_coords))
        {
            //MapGridSquare square = grid[(int)neighbour.x][(int)neighbour.y];
            //if (square.wall_neighbours.Intersect(square.orthagonal_neighbour_coords).Count() == 3)
            //{
            //    return false;
            //}

            if (wallCoordList.Contains(newWall.Opposite(neighbour)))
            {
                return false;
            }
        }
        return true;
    }
    private bool LargeCShapeCheck(MapGridSquare newWall) 
    {
        foreach (Vector2 neighbour in newWall.path_neighbours.Intersect(newWall.orthagonal_neighbour_coords)) 
        {
            MapGridSquare neighbourSquare = GetSquare(neighbour);
            foreach (Vector2 knightsquare in neighbourSquare.diagonal_neighbour_coords.Intersect(neighbourSquare.wall_neighbours)) 
            {
                if (!newWall.orthagonal_neighbour_coords.Contains(knightsquare) && 
                    pathCoordList.Intersect(GetSquare(knightsquare).orthagonal_neighbour_coords.Intersect(newWall.diagonal_neighbour_coords)).Count() > 0) 
                {
                    return false;
                }
            }
        }
        return true;
    }
    private bool DiagonalGapCheck(MapGridSquare newWall) 
    {
        foreach (Vector2 pathDiagNeighbour in newWall.path_neighbours.Intersect(newWall.diagonal_neighbour_coords)) 
        {
            if ((GetSquare(pathDiagNeighbour).path_neighbours.Intersect(newWall.path_neighbours).Count() == 3) && 
                wallCoordList.Contains(newWall.Opposite(pathDiagNeighbour))) 
            {
                return false;
            }
        }
        return true;
    }

    //private void CheckForColliderGaps(MapGridSquare newWall) 
    //{
    //    foreach ()
    //}
    // End of Wall During Checks



    // Start of Tank During Checks
    private bool PlayerProxCheck(MapGridSquare newWall)
    {
        if (newWall.type != Type.player)
        {
            if (Vector2.Distance(newWall.coord, playerRunTimeCoord) <= MIN_DISTANCE_FROM_PLAYER) 
            {
                return false;
            }
        }
        return true;
    }
    private bool TankProxCheck(MapGridSquare newWall) 
    {
        foreach (Vector2 tankCoord in tankCoordList.Where(x => x != newWall.coord).ToList()) 
        {
            if (Vector2.Distance(newWall.coord, tankCoord) <= MIN_DISTANCE_FROM_TANK)
            {
                return false;
            }
        }
        return true;
    }
    // End of Tank During Checks




    // Start of Helper Methods
    public CompressedMapGrid GetCompressedMap() 
    {
        CompressedMapGrid compressedMap;
        compressedMap.width = width;
        compressedMap.height = height;
        compressedMap.tankArray = tankCoordList.Select(Vector2ToIndex).ToArray();
        compressedMap.wallArray = wallCoordList.Select(Vector2ToIndex).ToArray();
        compressedMap.tankTypeArray = tankList.Select(TankContainerToInt).ToArray();
        return compressedMap;
    }
    public void Display()
    {
        string printed_string = "";
        for (int i_height = 0; i_height < height; i_height++)
        {
            for (int i_width = 0; i_width < width; i_width++)
            {
                printed_string += grid[i_width][i_height].type.GetDescription() + " ";
            }
            printed_string += "\n";
        }

        Debug.Log(printed_string);
    }
    public void FailedDisplay() 
    {
        string printed_string = "";
        for (int i_height = 0; i_height < height; i_height++)
        {
            for (int i_width = 0; i_width < width; i_width++)
            {
                if (grid[i_width][i_height].type == Type.wall)
                {
                    printed_string += grid[i_width][i_height].type.GetDescription() + " ";
                }
                else 
                {
                    printed_string += grid[i_width][i_height].failedcheck.GetDescription() + " ";
                }
            }
            printed_string += "\n";
        }

        Debug.Log(printed_string);
    }
    public List<Type> GetGridList()
    {
        List<Type> list = new();

        void AddRow(int row)
        {
            list.Add(Type.wall);
            for (int i = 0; i < width; i++)
            {
                list.Add(grid[width][row].type);
            }
            list.Add(Type.wall);
        }

        list.AddRange(Enumerable.Repeat(Type.wall, width + 2));
        for (int i = 0; i < height; i++)
        {
            AddRow(i);
        }
        list.AddRange(Enumerable.Repeat(Type.wall, width + 2));

        return list;
    }
    public Vector2 IndexToVector2(int index)
    {
        return new Vector2(index % width, index / width);
    }
    public int Vector2ToIndex(Vector2 vector2)
    {
        return (int)vector2.x + (int)vector2.y * width;
    }
    private int TankContainerToInt(TankContainer tank) 
    {
        return (int)tank.tankType;
    }
    // End of Helper Methods




    // Start of RunTime Methods
    public Vector2 ToGridPosition(Vector3 gamePosition)
    {
        float maxX = (width / 2f) * squareSize - 1;
        float maxY = (height / 2f) * squareSize - 1;
        return new Vector2(
            Mathf.RoundToInt(
            Mathf.Clamp(gamePosition.x, -1f * maxX, maxX) 
            / squareSize 
            + ((width - 1f) / 2f)
            ), 
            Mathf.RoundToInt(
            ((height - 1f) / 2f) - 
            Mathf.Clamp(gamePosition.z, -1f * maxY, maxY) 
            / squareSize
            )
        );
    }
    public Vector3 ToGamePosition(Vector2 gridPosition, float y_value)
    {
        return new Vector3((gridPosition.x - (width - 1f) / 2f) * squareSize, y_value, ((height - 1f) / 2f - gridPosition.y) * squareSize);
    }
    private Queue<Vector3> ShortestPath(Vector2 startGridPosition, Vector2 endGridPosition, List<Vector2> temp_obstacles) 
    {
        Queue<Vector3> GetQueue(MapGridSquare startSquare, MapGridSquare endSquare)
        {
            Queue<Vector3> path = new();
            MapGridSquare currentNode = endSquare;
            //Vector3 prevCoord = endSquare.coord;

            while (currentNode != startSquare)
            {
                path.Enqueue(ToGamePosition(currentNode.coord, squareSize / 3f));
                currentNode = GetSquare(currentNode.parentSquare);
            }
            Vector3[] reversedArray = path.ToArray();
            Array.Reverse(reversedArray);

            Queue<Vector3> reversedPath = new(reversedArray);
            return reversedPath;
        }

        foreach (Vector2 coord in pathCoordList) 
        {
            GetSquare(coord).cost = int.MaxValue;
        }

        Vector2 nearestNode = startGridPosition;
        PriorityQueue openSet = new();
        // Add the start node to the open set
        GetSquare(startGridPosition).cost = 0;
        openSet.Enqueue(GetSquare(startGridPosition));

        // Loop until the open set is empty
        while (!openSet.IsEmpty())
        {
            // Remove the node with the lowest cost value from the open set
            MapGridSquare currentNode = openSet.Dequeue();

            // If we've reached the end node, we're done
            if (currentNode.coord == endGridPosition)
            {
                return GetQueue(GetSquare(startGridPosition), GetSquare(endGridPosition));
            }

            // Expand the current node's neighbors
            foreach (Vector2 neighbor in currentNode.path_neighbours.Intersect(currentNode.orthagonal_neighbour_coords).Except(obstacleCoordList.Union(temp_obstacles)))
            {
                int newCost = currentNode.cost + 1; // Assuming a uniform cost of 1 for each step

                if (newCost < GetSquare(neighbor).cost)
                {
                    GetSquare(neighbor).cost = newCost;
                    GetSquare(neighbor).parentSquare = currentNode.coord;

                    // Add the neighbor to the open set if it's not already there
                    if (!openSet.Contains(GetSquare(neighbor)))
                    {
                        openSet.Enqueue(GetSquare(neighbor));
                    }

                    if (Vector2.Distance(neighbor, endGridPosition) < Vector2.Distance(nearestNode, endGridPosition))
                    {
                        nearestNode = neighbor;
                    }
                }
            }
        }

        // If we get here, there is no path from start to end
        return GetQueue(GetSquare(startGridPosition), GetSquare(nearestNode));
    }
    private List<Vector2> AvoidPath() 
    {
        List<Vector2> pathObstacles = new();
        foreach (TankContainer tank in TankList) 
        {
            foreach (Vector3 node in tank.currentPath) 
            {
                if (!pathObstacles.Contains(node)) { pathObstacles.Add(ToGridPosition(node)); }
            }
            Vector3 currentposition = tank.currentLocation;
            if (!pathObstacles.Contains(currentposition)) { pathObstacles.Add(ToGridPosition(currentposition)); }
            Vector3 currentDestination = tank.currentDestination;
            if (!pathObstacles.Contains(currentDestination)) { pathObstacles.Add(ToGridPosition(currentDestination)); }
        }
        return pathObstacles;
    }
    public void UpdatePlayerPosition(Vector3 gamePosition)
    {
        playerRunTimeCoord = ToGridPosition(gamePosition);
    }
    public void AddTankObstatcle(Vector3 obstacleLocation) 
    {
        obstacleCoordList.Add(ToGridPosition(obstacleLocation));
    }
    public void RemoveTankObstacle(Vector3 obstacleLocation) 
    {
        obstacleCoordList.Remove(ToGridPosition(obstacleLocation));
    }
    public Queue<Vector3> GetPath(Vector3 currentPosition, bool player)
    {
        Vector2 startPosition = ToGridPosition(currentPosition);
        Vector2 destination;
        if (player)
        {
            destination = playerRunTimeCoord;
        }
        else 
        {
            List<Vector2> freeSquareCoordList = new(pathCoordList.Except(obstacleCoordList));
            destination = freeSquareCoordList[UnityEngine.Random.Range(0, freeSquareCoordList.Count)];
        }

        return ShortestPath(startPosition, destination, AvoidPath());
    }
    public Queue<Vector3> GetPathTo(Vector3 currentPosition, Vector3 desiredLocation) 
    {
        Vector2 startPosition = ToGridPosition(currentPosition);
        Vector2 destination = ToGridPosition(desiredLocation);

        return ShortestPath(startPosition, destination, AvoidPath());
    }

    public List<Vector2> ToVector2List(List<Vector3> vector3List) 
    {
        return vector3List.Select(v => ToGridPosition(v)).ToList();
    }
    public List<Vector3> ToVector3List(List<Vector2> vector2List, float y_value)
    {
        return vector2List.Select(v => ToGamePosition(v, y_value)).ToList();
    }
    //End of RunTime Methods
}


public enum Type
{
    [Description("_")]
    path = 0,
    [Description("X")]
    wall = 2,
    [Description("O")]
    tank = 3,
    [Description("P")]
    player = 4
}

public enum FailedCheck
{
    [Description("C")]
    chunkDistanceCheck = 0,
    [Description("B")]
    chunkBoundaryDistance = 1,
    [Description("M")]
    maxChunkCheck = 2,
    [Description("D")]
    diagonalToWallCheck = 3,
    [Description("S")]
    cShapeCheck = 4,
    [Description("Z")]
    pathConnectivityCheck = 5,
    [Description("N")]
    maxNeighbourCheck = 6,
    [Description("L")]
    largeCShapeCheck = 7,
    [Description("G")]
    diagonalGapCheck = 8,
    [Description("_")]
    none = 9
}

public struct CompressedMapGrid 
{
    public int width, height;
    public int[] wallArray, tankArray, tankTypeArray;
}

public static class EnumExtensions
{
    public static string GetDescription(this Type value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
    public static string GetDescription(this FailedCheck value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
    public static float GetProbability(this TankType value, int level_no)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = fieldInfo.GetCustomAttributes(typeof(ProbabilityAttribute), false) as ProbabilityAttribute[];
        return attributes.Length > 0 ? attributes[0].Probability(level_no) : 0f;
    }
}
