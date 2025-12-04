using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Robot : MonoBehaviour
{
    public string robotCommand;
    public List<Person> myDirectFollowers;
    public int robotFollowerCounter;
    public MultiAgent myAgent;
    // bot的NavMeshAgent组件
    private NavMeshAgent _botNavMeshAgent;
    public bool isRunning;//机器人是否处于工作状态
    // Start is called before the first frame update
    public void Start()
    {
        this.gameObject.SetActive(true);
        isRunning = false;//机器人默认为不工作
        myDirectFollowers = new List<Person>();
        _botNavMeshAgent = GetComponent<NavMeshAgent>();
    }

}
