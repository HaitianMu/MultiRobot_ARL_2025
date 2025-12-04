using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.UI;

public class ComplexityControl : MonoBehaviour
{
    // Start is called before the first frame update
    public float MINROOMAREA = 5f; //最小的房间面积
    public BuildingControl buildingGeneration;  // 引用 BuildingGeneratiion 脚本
    public NavMeshSurface surface;//生成导航的组件
    public GameObject HumanList;
    public GameObject HumanPrefab;
   /* void Update()
    {
       
        // 每帧更新计时器
        timer += Time.deltaTime;

        // 如果计时器达到 1 秒
        if (timer >=interval)
        {
            Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            // 重置计时器
            timer = 0f;

            // 生成随机数并调用 BeginGeneration
            float number1 = 900; // 整个区域的面积
            int number2 = UnityEngine.Random.Range(5, 21); // 划分的房间数量

            BeginGeneration(number1, number2);//生成环境后

            surface.BuildNavMesh();//生成导航

            SetHuman();//放置人类

        }
    }*/

    public void BeginGenerationFangTree(float number1, int number2) 
    {
            if (number2 > 0)
            {
                float[] result = DivideNumberRandomly(number1, number2);

            //调试代码
            /*// 输出结果
            foreach (float part in result)
            {
                Debug.Log("（随机数生成）随机划分区域大小的结果： " + part);
            }*/

            // 将划分结果传递给 BuildingGeneratiion

            buildingGeneration.ClearPreviousRooms();
            buildingGeneration.roomAreas = result; // 设置房间面积数组
            buildingGeneration.GenerateRoomsFang();  // 调用房间生成方法
            }
            else
            {
                Debug.LogError("划分的数量必须>=1");
            }
    }


    public void BeginGenerationBinary(float number1, int number2)
    {

       // Debug.Log("BeginGenerationBinary");
        if (number2 > 0)
        {
            buildingGeneration.ClearPreviousRooms();
            buildingGeneration.GenerateRoomsBinary(number1,number2);  // 调用房间生成方法
        }
        else
        {
            Debug.LogError("划分的数量必须>=1");
        }
    }

    public void BeginGenerationJsonLoad(string filename,string layoutname)
    {
        // Debug.Log("BeginGenerationJson");
        
            buildingGeneration.ClearPreviousRooms();
            buildingGeneration.GenerateRoomsJsonLoad( filename,layoutname);  // 调用房间生成方法
        
    }
    // 随机将 number1 划分为 number2 个部分
    //采用平滑分配策略：每个房间的基础面积 baseArea 被均匀计算为总面积除以房间数量。然后在此基础上应用随机的调整值，确保分配相对平衡。
    //波动范围：：波动范围 maxAdjustment 被设置为剩余面积的一半（remaining / 2f），这样可以避免极端的随机变化。并且通过 rand.NextDouble() 控制波动的幅度
    public float[] DivideNumberRandomly(float number1, int number2)
    {
        if (number2 <= 0)
        {
            throw new ArgumentException("Number of parts must be greater than 0", nameof(number2));
        }

        float[] result = new float[number2];

        // 随机数生成器
        System.Random rand = new System.Random();

        // 计算每个房间的初始平均面积
        float baseArea = number1 / number2;

        // 计算剩余的面积
        float remaining = number1;

        // 随机生成房间面积
        for (int i = 0; i < number2 - 1; i++)
        {
            // 计算允许的最大波动范围,波动范围的最大值有上限，但最小值没有
            //这里计算最大值考虑最差的情况，即前n-1个房间全部取最大偏差时，最后一个房间的面积仍然大于0；
            //波动范围的最小值可以依据房间面积的最小值来计算，规定房间最小应大于5m2(存储室)，波动范围最小值应该是平均面积-5
            //但由于取到最差情况的概率同样会随着房间数目的增大而减小，我们可以适当扩大这个变化，否则num2越大，越接近平均分配

            int T = Mathf.RoundToInt((number2 / 10));//设置T来适当扩大波动范围
            if (T < 1) { T = 1; }
            float maxAdjustment = T * number1 / ((number2 - 1) * number2);
            float minAdjustment = baseArea - 5;

            // 随机生成调整值，控制调整幅度
            //rand.NextDouble()生成的范围是（0,1）   rand.NextDouble()*2-1 后将范围变为（-1,1）
            //生成每个房间的随机偏差值
            float change = (float)(rand.NextDouble() * 2 - 1);
            float adjustment;//用于记录房间的波动面积；

            if (change >= 0) { adjustment = change * maxAdjustment; }
            else { adjustment = change * minAdjustment; }

            //Debug.Log("房间面积的偏差为：" + adjustment);调试用
            // 计算每个房间的面积，确保至少为 minValue
            float roomArea = baseArea + adjustment;

            result[i] = Mathf.Round(roomArea); // 四舍五入为整数

            remaining -= result[i]; // 更新剩余面积
        }

        // 计算最后一个房间的面积，确保总面积不超过 number1，且大于0
        result[number2 - 1] = remaining;
        return result;

    }

    //随机位置生成10个人类，
    /*public void SetHuman()
    {
        //
        HumanList = GameObject.Find("HumanList");
        
        for (int i = 0; i < 10; i++)
        {
            float randomX = UnityEngine.Random.Range(1f, buildingGeneration.totalWidth);
            float randomZ = UnityEngine.Random.Range(1f, buildingGeneration.totalHeight);
            GameObject Person=  Instantiate(HumanPrefab,new Vector3(randomX,0.5f,randomZ), Quaternion.identity);
           Person.transform.parent = HumanList.transform;
        }
    }*/


}
