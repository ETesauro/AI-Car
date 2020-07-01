using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class CarAgent : Agent
{
    private float DirHorizontal;
    private float DirVertical;
    private float steerAngle;
    public WheelCollider WheelCFR, WheelCFL, WheelCBR, WheelCBL;
    public Transform WheelTFR, WheelTFL, WheelTBR, WheelTBL;
    public float maxSteerAngle = 30;
    public float motorForce = 300;
    public float brakes = 0;

    private Rigidbody rBody;
    public GameObject checkpoint;


    public GameObject pedone;
    public Waypoint[] waypoints;
    private WaypointNavigator waypointNavigator;

    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
        waypointNavigator = pedone.GetComponent<WaypointNavigator>();
    }
    
    public override void OnEpisodeBegin()
    {
        ResetCar();
        ResetPedone();

        Debug.Log(GetCumulativeReward());
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //+3
        sensor.AddObservation(transform.localPosition);
        //+3
        sensor.AddObservation(transform.localRotation);
        //+3
        sensor.AddObservation(rBody.velocity);

        //+3
        sensor.AddObservation(checkpoint.transform.localPosition);

        //+3
        sensor.AddObservation(pedone.transform.localPosition);
    }

    public override void Heuristic(float[] actionsOut)
    {
        DirHorizontal = Input.GetAxis("Horizontal");
        DirVertical = Input.GetAxis("Vertical");

        actionsOut[0] = DirHorizontal;
        actionsOut[1] = DirVertical;

        //ferma la macchina
        if (Input.GetKey(KeyCode.Space))
        {
            actionsOut[2] = 0f;
        }
        else
            actionsOut[2] = 1f;

    }

    public override void OnActionReceived(float[] vectorAction)
    {
        if (vectorAction[1] == 0 || Input.GetKey(KeyCode.Space))
        {
            brakes = 15000;
        }
        else if(vectorAction[1] >= 0)
        {
            brakes = 0;
            WheelCFR.motorTorque = vectorAction[1] * motorForce;
            WheelCFL.motorTorque = vectorAction[1] * motorForce;
            WheelCBR.motorTorque = vectorAction[1] * motorForce;
            WheelCBL.motorTorque = vectorAction[1] * motorForce;
            AddReward(0.1f);
        }

        WheelCFR.brakeTorque = brakes;
        WheelCFL.brakeTorque = brakes;
        WheelCBR.brakeTorque = brakes;
        WheelCBL.brakeTorque = brakes;

        //ferma immediatamente la macchina
        /*if (vectorAction[2] <= 0.5f)
        {
            rBody.velocity = new Vector3(vectorAction[2], vectorAction[2], vectorAction[2]);
            rBody.angularVelocity = new Vector3(vectorAction[2], vectorAction[2], vectorAction[2]);
        }*/

        //Se va indietro assegno un bonus negativo
        if (vectorAction[1] < 0)
        {
            AddReward(-10f);
        }
    }


    // --------- CONTROLLI MACCHINA ---------

    public void steerControl()
    {
        steerAngle = maxSteerAngle * DirHorizontal;
        WheelCFR.steerAngle = steerAngle;
        WheelCFL.steerAngle = steerAngle;
    }

    public void wheelPositionControl()
    {
        wheelPositionControls(WheelCFR, WheelTFR);
        wheelPositionControls(WheelCFL, WheelTFL);
        wheelPositionControls(WheelCBR, WheelTBR);
        wheelPositionControls(WheelCBL, WheelTBL);
    }

    public void wheelPositionControls(WheelCollider collide, Transform trans)
    {
        Vector3 wheelPosition = trans.position;
        Quaternion wheelQuant = trans.rotation;

        collide.GetWorldPose(out wheelPosition, out wheelQuant);

        trans.position = wheelPosition;
        trans.rotation = wheelQuant;
    }

    // --------- FINE CONTROLLI MACCHINA ---------

    public void FixedUpdate()
    {
        steerControl();
        wheelPositionControl();
        debug_drawDestination();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Checkpoint")
        {
            AddReward(1.0f);
            EndEpisode();
        }

        if (other.tag == "People")
        {
            AddReward(-1000f);
            EndEpisode();
        }

        if (other.tag == "Wall")
        {
            AddReward(-0.1f);
            EndEpisode();
        }
    }

    private void ResetCar()
    {
        transform.localPosition = new Vector3(2.72f, 0.2f, 0f);
        transform.rotation = Quaternion.identity;
        rBody.velocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;
    }

    private void ResetPedone()
    {
        waypointNavigator.currentWaypoint = waypoints[Random.Range(0, waypoints.Length - 1)];
        pedone.transform.position = waypointNavigator.currentWaypoint.transform.position;
    }

    protected void debug_drawDestination()
    {
        Debug.DrawLine(transform.position, checkpoint.transform.position, Color.yellow);
    }
}