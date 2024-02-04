using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class Inputs : MonoBehaviour
{
    public GameObject simaltion;
    public GameObject ground;
    public GameObject sphere;
    private SPH sph;
    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        sph = simaltion.GetComponent<SPH>();
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Z) && Input.GetKey(KeyCode.LeftShift) && sph.boxSize.x >= 2)
        {
            sph.boxSize.x -= 0.01f;
            ground.transform.localScale -= new Vector3(0.01f, 0, 0);
        } else if (Input.GetKey(KeyCode.Z))
        {
            sph.boxSize.x += 0.01f;
            ground.transform.localScale += new Vector3(0.01f, 0, 0);
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (Input.GetKey(KeyCode.W))
            {
                sphere.transform.position += new Vector3(0, 0, 0.01f);
            }
            if (Input.GetKey(KeyCode.A))
            {
                sphere.transform.position += new Vector3(0.01f, 0, 0);
            }
            if (Input.GetKey(KeyCode.S))
            {
                sphere.transform.position -= new Vector3(0, 0, 0.01f);
            }
            if (Input.GetKey(KeyCode.D))
            {
                sphere.transform.position -= new Vector3(0.01f, 0, 0);
            }
        }
    }
}
