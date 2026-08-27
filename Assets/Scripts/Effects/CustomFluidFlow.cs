using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CustomFluidFlow : MonoBehaviour
{
    [Range(-0.5f,0.5f)]
    public float OffsetSlider;
    
    public Texture Green,Red;
    public bool isRed = true;
    public Material material;

    private void Update()
    {
        if(isRed)material.mainTexture = Red;else{material.mainTexture = Green;}
        material.mainTextureOffset = new Vector2(0, OffsetSlider);
    }
}
