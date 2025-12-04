using UnityEngine;
using System.Collections.Generic;

public class FirePoolManager : MonoBehaviour
{
    public static FirePoolManager Instance;
    public GameObject firePrefab;
    public int initialPoolSize = 100;
    public int maxPoolSize = 1000;

    private Queue<GameObject> firePool = new Queue<GameObject>();
    private Transform fireParent;

    void Awake()
    {
        Instance = this;
        fireParent = GameObject.Find("FireList").transform;
        InitializePool();
    }

    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject fire = Instantiate(firePrefab, fireParent);
            fire.SetActive(false);
            firePool.Enqueue(fire);
        }
    }

    public GameObject GetFire(Vector3 position, Quaternion rotation, EnvControl env)
    {
        GameObject fire;
        if (firePool.Count > 0)
        {
            fire = firePool.Dequeue();
        }
        else if (fireParent.childCount < maxPoolSize)
        {
            fire = Instantiate(firePrefab, fireParent);
        }
        else
        {
            return null; // 达到最大数量限制
        }

        // 确保 fire 对象没有被销毁
        if (fire == null)
        {
            Debug.LogError("火焰实例已经被销毁了");
            return null;
        }

        fire.transform.position = position;
        fire.transform.rotation = rotation;
        fire.SetActive(true);

        FireControl fireControl = fire.GetComponent<FireControl>();
        fireControl.myEnv = env;
        env.FireList.Add(fireControl);

        return fire;
    }

    public void ReturnFire(GameObject fire)
    {
        if (fire == null)
        {
            Debug.LogWarning("Trying to return a null fire object.");
            return;
        }
        

        fire.SetActive(false);
        firePool.Enqueue(fire);
    }
    public void ClearPool()
    {
        while (firePool.Count > 0)
        {
            var fire = firePool.Dequeue();
            if (fire != null)
            {
                Destroy(fire);
            }
        }
    }
}