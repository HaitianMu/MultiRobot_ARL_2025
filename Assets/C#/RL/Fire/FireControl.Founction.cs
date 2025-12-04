using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public partial class  FireControl : MonoBehaviour
{
        /// <summary>
        /// 判断某个地板快是否能生成火焰
        /// </summary>
        /// <param name="targetGroundBlock"></param>
        /// <returns></returns>


    public void CheckSurroundingStatus()
    //1,不越界；
    //2.不与别的火焰发生碰撞/不与墙壁发生碰撞/不与出口发生碰撞
    //3. 如果已经达到最大火焰数量，不再扩散
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = _directionSequence[i];
            Vector3 checkPos = transform.position + direction;

            // 先用空间分区快速检查
            _surroundingsStatus[i] = !fireSpatialPartition.HasNearbyFire(checkPos, 0.4f);

            // 如果空间分区检查通过，再进行精确检测
            if (_surroundingsStatus[i])
            {
                _surroundingsStatus[i] = CanSpawnAtPosition(checkPos);
            }
        }

        // 更新是否还能扩散
        isPossibleToSpread = _surroundingsStatus[0] || _surroundingsStatus[1] ||
                            _surroundingsStatus[2] || _surroundingsStatus[3];
        /*       if (activeFireCount >= maxActiveFires)
               {
                   _surroundingsStatus = new List<bool> { false, false, false, false };
                   return;
               }

               //1,不越界；
               //2.不与别的火焰发生碰撞/不与墙壁发生碰撞/不与出口发生碰撞

               *//*if (Physics.Raycast(FirePosition, Vector3.up, out RaycastHit hit, 1.0f))
                   return !(hit.transform.CompareTag("Fire") || hit.transform.CompareTag("Wall") || hit.transform.CompareTag("Exit"));
               else
                   return true;*//*



               //检测四周
               for (int i = 0; i < 4; i++)
               {
                   Vector3 direction = _directionSequence[i];

                   Vector3 halfExtents = new Vector3(0.249f, 4f, 0.249f);
                   Collider[] hits = Physics.OverlapBox(this.transform.position+direction, halfExtents, Quaternion.identity);


                       foreach (Collider col in hits)
                       {
                       if (col.tag == "Wall" || col.tag == "Fire" || col.tag == "Exit") //扩散的地方存在墙或者已经有火焰存在了，则该位置不能生成火焰
                        { _surroundingsStatus[i] = false;
                           print($"检测到物体: {col.name} (Tag: {col.tag})"+"，该位置不能进行扩散");
                           }


                       else if (col.tag == "Door")//发现了门
                       {
                               _surroundingsStatus[i] = true;
                               string doorname= col.name;
                               GameObject gameObject = GameObject.Find(doorname);
                               gameObject.SetActive(false);
                           print($"检测到物体: {col.name} (Tag: {col.tag})" + "，将门设置为不可用");
                       }

                   }

               }
               // 更新是否还能扩散
               isPossibleToSpread = _surroundingsStatus[0] || _surroundingsStatus[1] ||_surroundingsStatus[2] || _surroundingsStatus[3];
          */
    }
    /// <summary>
    /// 判断当前地板块上的火焰是否能向四周扩散
    /// </summary>
    /// <returns></returns>

    private IEnumerator TrySpreadFire()
    {
        for (int i = 0; i < 4; i++)
        {
            if (_surroundingsStatus[i])
            {
                Vector3 targetPos = transform.position + _directionSequence[i];

                // 使用空间分区快速检查
                if (!fireSpatialPartition.HasNearbyFire(targetPos, 0.4f))
                {
                    // 精确检测
                    if (CanSpawnAtPosition(targetPos))
                    {
                        // 从对象池获取火焰
                        GameObject newFire = FirePoolManager.Instance.GetFire(
                            targetPos,
                            Quaternion.identity,
                            myEnv);

                        if (newFire != null)
                        {
                            yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));
                        }
                    }
                }
            }
        }
    }

    private bool CanSpawnAtPosition(Vector3 position)
    {
        HashSet<string> tags = new HashSet<string>();
        List<GameObject> doorsFound = new List<GameObject>(); // 存储找到的门对象
        bool hasProcessedDoor = false; // 标记是否已处理过门

        Collider[] colliders = Physics.OverlapBox(
            position,
            new Vector3(0.2f, 4f, 0.2f),
            Quaternion.identity);

        // 第一遍遍历：收集标签和门对象
        foreach (var col in colliders)
        {
            string currentTag = col.tag;
            tags.Add(currentTag);

            if (currentTag == "Door"|| currentTag == "BurnedDoor" && !hasProcessedDoor)
            {
                doorsFound.Add(col.gameObject);
            }
        }

        // 第二遍遍历：处理门对象
        foreach (GameObject door in doorsFound)
        {
            // 确保门对象仍然有效
            if (door == null) continue;

            door.GetComponent<DoorControl>().BurnDoor();//烧毁门
        }

        // 判断逻辑（保持原有规则）
        bool hasDoor = tags.Contains("Door")|| tags.Contains("BurnedDoor");
        bool hasWall = tags.Contains("Wall");
        bool hasExit = tags.Contains("Exit");
        bool onFloor = tags.Contains("Floor");
        // 当同时存在门和墙时允许生成火焰
        if (hasDoor && hasWall) return true;
        // 单独存在墙或出口时禁止生成
        if (hasWall || hasExit) return false;

        // 其他情况,如果处于地板上，则允许生成
        if (onFloor)
        {
            return true;
        }

        return false;
    }

}
