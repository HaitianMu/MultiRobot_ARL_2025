using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UImanager : MonoBehaviour
{
    // Start is called before the first frame update
    // 引用输入框组件
    public TMP_InputField inputField1;  //区域大小
    public TMP_InputField inputField2;  //房间数量
    public float MINROOMAREA = 5f; //最小的房间面积

    // 引用按钮组件
    public Button submitButton;

    // 用于存储输入的数字
    private float number1; //整个区域的面积可以是小数
    private int number2;  //划分的房间数量一定是整数

    public BuildingGeneratiion buildingGeneration;  // 引用 BuildingGeneratiion 脚本

    // 按钮点击事件处理函数
    public void OnSubmit() //设置为public，以对外界可见，将该函数与按钮的onclick（）相关联，表示点击按钮即触发这个函数
    {
        // 尝试解析输入的字符串为数字
        if (float.TryParse(inputField1.text, out number1) && int.TryParse(inputField2.text, out number2))
        {
            // 输出读取的数字
            Debug.Log("区域大小为: " + number1);
            Debug.Log("房间数量为: " + number2);
            // 你可以在这里进行后续的计算或其他操作
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
                buildingGeneration.roomAreas = result; // 设置房间面积数组
                buildingGeneration.GenerateRooms();  // 调用房间生成方法
            }
            else
            {
                Debug.LogError("划分的数量必须>=1");
            }
        }
        else
        {
            // 如果解析失败，提示用户输入无效
            Debug.LogError("输入无效，请输入有效的数字！");
        }
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
            float maxAdjustment = T*number1 /((number2-1)*number2);
            float minAdjustment = baseArea-5;

            // 随机生成调整值，控制调整幅度
            //rand.NextDouble()生成的范围是（0,1）   rand.NextDouble()*2-1 后将范围变为（-1,1）
            //生成每个房间的随机偏差值
            float change= (float)(rand.NextDouble()*2-1);
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
}
