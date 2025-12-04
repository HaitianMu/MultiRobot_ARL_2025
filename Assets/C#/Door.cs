using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class Door : MonoBehaviour
{
    public string doorDirection; // 门的朝向

    public void Start()
    {
        DetermineDoorDirection();
    }
    void DetermineDoorDirection()//初始化门的朝向函数
    {
        Vector3 myPosition = transform.position;
        float checkRadius = 0.5f; // 统一的检测半径
        float checkDistance = 1.5f; // 检测距离

        // 检查左侧
        bool hasLeftCollider = Physics.CheckSphere(myPosition + Vector3.left * checkDistance, checkRadius);
        // 检查右侧
        bool hasRightCollider = Physics.CheckSphere(myPosition + Vector3.right * checkDistance, checkRadius);

        // 判断门的朝向
        if (hasLeftCollider && hasRightCollider)
        {
            doorDirection = "Horizontal";//垂直
        }
        else
        {
            doorDirection = "Vertical";//水平
        }

    }


}

