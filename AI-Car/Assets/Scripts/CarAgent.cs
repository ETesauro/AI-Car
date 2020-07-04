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


    private float maxZdist;
    private float xDist, zDist;
    private bool xCheck, zCheck;
    private Vector3 toTarget;
    private bool manualBrake = false, pedoneCheck;

    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
        waypointNavigator = pedone.GetComponent<WaypointNavigator>();
    }

    public override void OnEpisodeBegin()
    {
        ResetCar();
        ResetPedone();
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
            actionsOut[2] = 1f;
        }
        else
            actionsOut[2] = 0f;

    }

    public override void OnActionReceived(float[] vectorAction)
    {
        // vectorAction[0] -> destra/sinistra
        // vectorAction[1] -> avanti/dietro
        // vectorAction[2] -> frena

        //Accelera
        if (vectorAction[1] == 0 || Input.GetKey(KeyCode.Space))
        {
            brakes = 15000;
        }
        else if (vectorAction[1] >= 0)
        {
            brakes = 0;
            WheelCFR.motorTorque = vectorAction[1] * motorForce;
            WheelCFL.motorTorque = vectorAction[1] * motorForce;
            WheelCBR.motorTorque = vectorAction[1] * motorForce;
            WheelCBL.motorTorque = vectorAction[1] * motorForce;
            AddReward(0.1f);
        }

        //Controllo se incrocia un pedone; true=l'ha incrociato, false=non l'ha incrociato
        pedoneCheck = CheckPedoni();

        //Controllo se la macchina va all'indietro nel caso in cui non ha incrociato un pedone. Se sì, aggiungo un reward negativo
        if (vectorAction[1] < 0 && !pedoneCheck)    AddReward(-5f);
        else AddReward(0.1f);

        //Controllo se la macchina va avanti nel caso in cui non ha incrociato un pedone. Se sì, aggiungo un reward positivo
        if (vectorAction[1] > 0 && !pedoneCheck)    AddReward(10f);

        //Azioni da eseguire se si incrocia un pedone
        if (pedoneCheck)
        {

            // --------- SISTEMARE DA QUI ---------
            //Creare un metodo che restituisce true se la posizione della macchina è avanti sull'asse z rispetto a quella del pedone
            //Mi serve per dire alla macchina di non frenare e di accelerare altrimenti rimarrebbe ferma e il pedone le andrebbe a sbattere addosso

            //Se il pedone è sull'asse x vicino alla macchina
            if ((pedone.transform.position.x - transform.position.x) >= -5.8f && (pedone.transform.position.x - transform.position.x) <= 4.6f)
            {
                 //Se il pedone è sull'asse z AVANTI alla macchina allora la macchina deve fermarsi
                if (pedone.transform.position.z >= transform.position.z)
                {
                    //rBody.velocity = Vector3.zero;
                    vectorAction[2] = 1;
                    AddReward(30f);
                }
                //Se la macchina è avanti rispetto al pedone (Lo ha già superato ma lui sta attraversando lo stesso le strisce)
                else
                {
                    Debug.Log("qui");
                    pedone.transform.position = new Vector3(-5f, pedone.transform.position.y, pedone.transform.position.z);
                    vectorAction[2] = 0;
                    vectorAction[1] = 1;
                }
            }

            // --------- FINO A QUI ---------
            else
            {
                vectorAction[2] = 0;
            }

            if (getVelocitySpeed() < 3 && getVelocitySpeed() > -1)
            {
                Debug.Log("Correct Speed");
                AddReward(maxZdist * 20f);
            }
            else
            {
                AddReward(-10f);
            }
        }
        else if (getVelocitySpeed() < 3) AddReward(-100f);

        //Frena
        if (vectorAction[2] >= 0.5)
        {
            manualBrake = true;
            //vectorAction[1] = 0;
            frena(1f);
        }
        else
        {
            manualBrake = false;
            brakes = 0;
        }

        //Se frena nella posizione sbagliata (lontano dal pedone)
        if (!pedoneCheck && manualBrake)
        {
            Debug.Log("Wrong Brake");
            AddReward(-10f);
        }

        //Se frena nella posizione corretta (vicino al pedone)
        if (pedoneCheck && manualBrake)
        {
            Debug.Log("Correct Brake");
            AddReward(100f);
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

    private bool CheckPedoni()
    {

        maxZdist = 0;
        bool pedOnTrajectory = false;
        toTarget = (pedone.transform.position - transform.position).normalized;

        if (Vector3.Dot(toTarget, transform.forward) > 0)
        {
            xDist = (pedone.transform.position.x - transform.position.x) + 10;
            zDist = Mathf.Abs(pedone.transform.position.z - transform.position.z);

            xCheck = (-2 < xDist) && (xDist < 14);
            zCheck = (0 < zDist) && (zDist < 10);

            if (xCheck && zCheck)
            {
                //Debug.Log("pedastrian " + pedone.name + " is in a risk point for " + gameObject.name + " .");
                Debug.DrawLine(transform.position, pedone.transform.position, Color.blue);
            }

            if ((xCheck && zCheck) && !pedOnTrajectory)
            {
                if (zDist > maxZdist) maxZdist = zDist;
                pedOnTrajectory = true;
            }
        }

            return pedOnTrajectory;
    }

    protected float getVelocitySpeed()
    {
        //Debug.Log("velocity: " + transform.InverseTransformDirection(rBody.velocity).z);
        return transform.InverseTransformDirection(rBody.velocity).z;
    }

    protected void frena(float decrement)
    {
        WheelCFR.brakeTorque = brakes;
        WheelCFL.brakeTorque = brakes;
        WheelCBR.brakeTorque = brakes;
        WheelCBL.brakeTorque = brakes;
        diminuisciVelocita(1f, decrement);
    }

    protected void diminuisciVelocita(float toCheck, float decrement)
    {
        if ((rBody.velocity.x > toCheck || rBody.velocity.x < toCheck * (-1)) || (rBody.velocity.y > toCheck || rBody.velocity.y < toCheck * (-1))
            || (rBody.velocity.z > toCheck || rBody.velocity.z < toCheck * (-1)))
        {
            if (rBody.velocity.x > toCheck)
                rBody.velocity = new Vector3(rBody.velocity.x - decrement, rBody.velocity.y, rBody.velocity.z);
            if (rBody.velocity.x < toCheck * (-1))
                rBody.velocity = new Vector3(rBody.velocity.x + decrement, rBody.velocity.y, rBody.velocity.z);
            if (rBody.velocity.y > toCheck)
                rBody.velocity = new Vector3(rBody.velocity.x, rBody.velocity.y - decrement, rBody.velocity.z);
            if (rBody.velocity.y < toCheck * (-1))
                rBody.velocity = new Vector3(rBody.velocity.x, rBody.velocity.y + decrement, rBody.velocity.z);
            if (rBody.velocity.z > toCheck)
                rBody.velocity = new Vector3(rBody.velocity.x, rBody.velocity.y, rBody.velocity.z - decrement);
            if (rBody.velocity.z < toCheck * (-1))
                rBody.velocity = new Vector3(rBody.velocity.x, rBody.velocity.y, rBody.velocity.z + decrement);
        }
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
            AddReward(100.0f);
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

        if (other.tag == "Strisce")
        {
            AddReward(0.1f);
        }

        Debug.Log("Colpito: " + other.gameObject.name);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Strisce")
        {
            AddReward(100f);
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