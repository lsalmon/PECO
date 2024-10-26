using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class InvestigateNoise : BehaviorBase, IListenToNoise {

    public Vector3[] lookAroundPoints;
    public float waitTime = 3f, noiseRange = 20f;

    private int currentPoint, investigationRange = 5, numPoints = 4;
    private bool investigating, lookAroundPointsFilled, looking;
    private NavMeshAgent agent;
    private PatrolBasic patrolScript;

    protected override void Start() {
        agent = GetComponent<NavMeshAgent>();
        patrolScript = GetComponent<PatrolBasic>();
        currentPoint = 0;
        lookAroundPoints = new Vector3[numPoints];
        lookAroundPointsFilled = false;
        investigating = false;
        looking = false;
    }

    public override void OnIdle() {
        if(investigating && AtDestination() && !looking) {
            if(!lookAroundPointsFilled) {
                agent.stoppingDistance = 0.0f;
                Vector3 initialPos = agent.transform.position;
                for(int i = 0; i < numPoints; i++) {
                    Vector2 scatter = Random.insideUnitCircle * investigationRange;
                    Vector3 newPoint = new Vector3(initialPos.x + scatter.x, initialPos.y, initialPos.z + scatter.y);
                    if(agent.CalculatePath(newPoint, new NavMeshPath())) {
                        lookAroundPoints[i] = newPoint;
                    } else {
                        lookAroundPoints[i] = initialPos;
                    }
                }
                lookAroundPointsFilled = true;
            }
            looking = true;
            this.NextDestination();
        }
    }

    protected IEnumerator StartWait() {
        currentPoint++;
        yield return new WaitForSeconds(waitTime);
        this.NextDestination();
    }

    protected void NextDestination() {
        if(currentPoint >= lookAroundPoints.Length) {
            currentPoint = 0;
            investigating = false;
            looking = false;
            lookAroundPointsFilled = false;
            StopAllCoroutines();
            patrolScript.ReturnToPatrol();
            return;
        }
        if(!agent.SetDestination(lookAroundPoints[currentPoint])) {
            currentPoint++;
        }
        StartCoroutine(this.StartWait());
    }

    protected bool AtDestination() {
        if(agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)) {
            return true;
        }
        return false;
    }

    public override void GoToNoise(Vector3 noisePosition) {
        base.susFlag = false;
        // Force suspicion state
        base.OnSuspicious();
        base.currentDetection = base.suspiciousLevel;
        patrolScript.InterruptPatrol();
        agent.isStopped = true;
        agent.SetDestination(noisePosition);
        agent.stoppingDistance = 10.0f;
        agent.isStopped = false;
    }

    public void OnPlayerMakesNoise(Vector3 playerPosition) {
        float distanceToPlayer = Vector3.Distance(playerPosition, this.transform.position);
        if(distanceToPlayer <= noiseRange && !investigating) {
            investigating = true;
            this.GoToNoise(playerPosition);
        }
    }

}
