using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CropTile
{
    public Vector3Int tilePosition;
    public CropData cropData;
    public int currentStage = 0;
    public float timer = 0f;
    public bool isWatered = false;
    public GameObject cropOverlayObject; 

    public CropTile(Vector3Int pos, CropData data, GameObject overlay)
    {
        tilePosition = pos;
        cropData = data;
        cropOverlayObject = overlay;
    }
}

