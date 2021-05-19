using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;

/* Populate a dynamic grid - used for app/media UI pages */
public class IconGrid : MonoBehaviour
{
    [SerializeField] private RectTransform panelRow;
    [SerializeField] private GameObject gridCell;
    [SerializeField] private int gridWidth = 3;

    /* Generate the grid of content */
    public void GenerateGrid(int app_count)
    {
        int height = (int)Mathf.Ceil((float)app_count / (float)gridWidth) ;

        for (int count = 0; count < gameObject.transform.childCount; count++)
        {
            Destroy(gameObject.transform.GetChild(count).gameObject);
        }

        GameObject cellInputField;
        RectTransform rowParent;
        int totalAdded = 0;
        for (int rowIndex = 0; rowIndex < height; rowIndex++)
        {
            rowParent = Instantiate(panelRow);
            rowParent.transform.SetParent(gameObject.transform);
            rowParent.transform.localScale = Vector3.one;
            for (int colIndex = 0; colIndex < gridWidth; colIndex++)
            {
                totalAdded++;
                if (totalAdded > app_count) break;
                cellInputField = Instantiate(gridCell);
                cellInputField.transform.SetParent(rowParent);
                cellInputField.transform.localScale = Vector3.one;
            }
            if (rowParent.childCount == 0) Destroy(rowParent.gameObject);
        }
    }
}