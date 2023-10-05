
/*
    A level map is a grid made up of MapGridSquares. This class is used by the class MapGrid to mainly work 
    out a grid square's neighbouring squares and other properties of the grid square.
*/


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;
using System.Linq;
using System;

public class MapGridSquare
{
    public Type type = Type.path;
    public Vector2 coord = Vector2.zero;
    public List<Vector2> wall_neighbours = new();
    public List<Vector2> path_neighbours; //values added in constructor
    public List<Vector2> neighbour_coords = new();
    public List<Vector2> orthagonal_neighbour_coords = new();
    public List<Vector2> diagonal_neighbour_coords = new();
    public FailedCheck failedcheck = FailedCheck.none;

    //RunTime Variables
    public Vector2 parentSquare;
    public int cost = int.MaxValue;

    public MapGridSquare(Type type, Vector2 coord, int height, int width)
    {
        this.type = type;
        this.coord = coord;


        int[] cols = { -1, 0, 1, -1, 0, 1, -1, 0, 1 };
        int[] rows = { -1, -1, -1, 0, 0, 0, 1, 1, 1 };

        int col = (int)coord.x;
        int row = (int)coord.y;

        for (int i = 0; i < rows.Length; i++)
        {
            int newRow = row + rows[i];
            int newCol = col + cols[i];
            if (newRow >= 0 && newRow < height && newCol >= 0 && newCol < width && !(newRow == row && newCol == col))
            {
                Vector2 neighbourCoord = new(newCol, newRow);

                neighbour_coords.Add(neighbourCoord);
                if (i % 2 == 0)
                {
                    diagonal_neighbour_coords.Add(neighbourCoord);
                }
                else
                {
                    orthagonal_neighbour_coords.Add(neighbourCoord);
                }


            }
        }

        path_neighbours = neighbour_coords.ToList();
    }


    public bool HasMaxedWallNeighbours(int maxvalue)
    {
        return (wall_neighbours.Count + (8 - neighbour_coords.Count) > maxvalue);
    }

    public bool IsCorner()
    {
        return diagonal_neighbour_coords.Count == 1;
    }

    public bool IsEdge()
    {
        return neighbour_coords.Count < 8;
    }

    public Vector2 Opposite(Vector2 coord1) 
    {
        return 2f * coord1 - coord;
    }
}
