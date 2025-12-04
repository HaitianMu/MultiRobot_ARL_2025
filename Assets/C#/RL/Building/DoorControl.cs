using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class DoorControl : MonoBehaviour
{
    public string doorDirection; // 门的朝向
    //!!!!!!!!!火焰与门的交互
    private NavMeshLink Link;     //导航组件，火焰扩散到门之后，禁用该导航组件并更新导航网格
    public bool isBurnt = false; //门是否被烧毁

    public void AddNavMeshLink()
    {
        Link = this.AddComponent<Unity.AI.Navigation.NavMeshLink>();
        Link.autoUpdate = true;
        Link.width = 1;
        Link.bidirectional = true;//允许双向导航

        if (doorDirection == "Vertical") //水平
        {
            Link.startPoint = new Vector3(0, -1.5f, 0.4f);
            Link.endPoint = new Vector3(0, -1.5f, -0.4f);
        }
        else if (doorDirection == "Horizontal")
        {
            Link.startPoint = new Vector3(0.4f, -1.5f, 0);
            Link.endPoint = new Vector3(-0.4f, -1.5f, 0);
        }
        // 特别重要！设置合适的下落距离
      /*  Link.height = 2.0f; // 允许的高度差*/
    }

    public void BurnDoor()
    {
        if (isBurnt || Link == null) return;
        // 视觉效果
        GetComponent<Renderer>().material.color = Color.black; //门颜色变成黑色
        //禁用门的导航功能
        // 2.处理导航链接（新版Unity方式）
        Link.enabled = false; // 禁用组件
        // 3. 完全移除导航连接
        Destroy(Link);
        // 4. 标记为已烧毁
        isBurnt = true;
        // 5.改变门的标签，防止再次被作为目标地点
        this.tag = "BurnedDoor";
        // 5. 更新导航网格
        StartCoroutine(DelayedNavMeshUpdate());
    }

    private IEnumerator DelayedNavMeshUpdate()
    {
        yield return new WaitForSeconds(0.5f); // 延迟更新避免卡顿

        // 获取场景中的NavMeshSurface并更新
        var surface = FindObjectOfType<NavMeshSurface>();
        if (surface != null)
        {
            surface.UpdateNavMesh(surface.navMeshData);
        }
    }
}
