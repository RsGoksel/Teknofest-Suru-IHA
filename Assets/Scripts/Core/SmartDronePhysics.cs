using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TEKNOFEST Sürü İHA - Akıllı Drone Fizik Sistemi
/// Tek drone'un tüm fizik ve hareket kontrolü
/// </summary>
public class SmartDronePhysics : MonoBehaviour
{
    [Header("Physics Parameters")]
    public float thrustForce = 12f;
    public float moveForce = 10f;
    public float hoverThrust = 10f;
    public float maxSpeed = 14f;
    public float fastModeMultiplier = 2f;
    
    [System.NonSerialized] public int droneID;
    
    // Components
    private DroneSpawner swarmController;
    private DroneCommHub commHub;
    private Rigidbody rb;
    
    // State Machine
    private enum DroneState { 
        Grounded, Armed, TakingOff, Hovering, Staging, FormationMove, 
        FastFormationMove, FormationHold, Landing 
    }
    
    private DroneState currentState = DroneState.Grounded;
    
    // Position Targets
    private Vector3 assignedPosition;
    private Vector3 stagingPosition;
    private float targetHeight;
    
    // Performance Tracking
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f;
    
    public void Initialize(float thrust, float move, int id, DroneSpawner controller, DroneCommHub hub)
    {
        thrustForce = thrust;
        moveForce = move;
        droneID = id;
        swarmController = controller;
        commHub = hub;
        
        rb = GetComponent<Rigidbody>();
        targetHeight = transform.position.y;
        lastPosition = transform.position;
        
        Debug.Log($"🚁 Drone {droneID} initialized - Thrust: {thrustForce}, Move: {moveForce}");
    }
    
    void FixedUpdate()
    {
        if (currentState == DroneState.Grounded) return;
        
        // Apply upward thrust
        float upwardThrust = CalculateIntelligentThrust();
        rb.AddForce(Vector3.up * upwardThrust);
        
        // Process movement
        ProcessIntelligentMovement();
        
        // Speed limiting
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        
        // Stuck detection
        CheckStuckState();
    }
    
    void ProcessIntelligentMovement()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetForce = Vector3.zero;
        
        switch (currentState)
        {
            case DroneState.Staging:
                targetForce = CalculateSmartMovement(currentPos, stagingPosition);
                CheckWaypointReached(stagingPosition, 2f);
                break;
                
            case DroneState.FormationMove:
                targetForce = CalculateSmartMovement(currentPos, assignedPosition);
                CheckWaypointReached(assignedPosition, 1.5f);
                break;
                
            case DroneState.FastFormationMove:
                targetForce = CalculateFastMovement(currentPos, assignedPosition);
                CheckWaypointReached(assignedPosition, 1.8f);
                break;
                
            case DroneState.FormationHold:
                targetForce = CalculatePrecisionHold(currentPos, assignedPosition);
                break;
        }
        
        rb.AddForce(targetForce);
    }
    
    /// <summary>
    /// Akıllı hareket hesaplama - çarpışma önleme ile
    /// </summary>
    Vector3 CalculateSmartMovement(Vector3 from, Vector3 to)
    {
        if (commHub == null) 
            return (to - from).normalized * moveForce;
        
        // Hub'dan güvenli yol iste
        Vector3 safeDirection = commHub.RequestSafePathVector(droneID, from, to);
        
        // Mesafe bazlı kuvvet ayarlama
        float distance = Vector3.Distance(from, to);
        float forceMultiplier = Mathf.Clamp(distance / 2f, 0.8f, 2f);
        
        return safeDirection * moveForce * forceMultiplier;
    }
    
    /// <summary>
    /// Hızlı hareket modu - formasyonlar için
    /// </summary>
    Vector3 CalculateFastMovement(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        Vector3 avoidance = CalculateBasicAvoidance(from);
        
        float forceMultiplier = Mathf.Clamp(distance / 1.5f, 1f, 2.5f);
        return (direction + avoidance * 0.2f).normalized * moveForce * fastModeMultiplier * forceMultiplier;
    }
    
    /// <summary>
    /// Basit çarpışma önleme - hub olmadan
    /// </summary>
    Vector3 CalculateBasicAvoidance(Vector3 myPos)
    {
        if (swarmController == null) return Vector3.zero;
        
        Vector3 avoidance = Vector3.zero;
        List<SmartDroneData> nearby = swarmController.GetNearbyDroneData(droneID, myPos, 4f);
        
        foreach (SmartDroneData drone in nearby)
        {
            if (drone.distance < 3f)
            {
                Vector3 away = (myPos - drone.position).normalized;
                avoidance += away * (3f - drone.distance);
            }
        }
        
        return avoidance;
    }
    
    /// <summary>
    /// Hassas pozisyon tutma - formasyon kilidi
    /// </summary>
    Vector3 CalculatePrecisionHold(Vector3 from, Vector3 to)
    {
        Vector3 error = to - from;
        float errorMagnitude = error.magnitude;
        
        if (errorMagnitude < 0.1f) return Vector3.zero;
        
        float force = Mathf.Clamp(errorMagnitude * 2f, 0.1f, moveForce * 0.5f);
        return error.normalized * force;
    }
    
    /// <summary>
    /// Waypoint'e ulaşma kontrolü
    /// </summary>
    void CheckWaypointReached(Vector3 target, float threshold)
    {
        if (Vector3.Distance(transform.position, target) < threshold)
        {
            switch (currentState)
            {
                case DroneState.Staging:
                    currentState = DroneState.Hovering;
                    Debug.Log($"🎯 Drone {droneID}: Staging position reached");
                    break;
                    
                case DroneState.FormationMove:
                case DroneState.FastFormationMove:
                    currentState = DroneState.Hovering;
                    targetHeight = assignedPosition.y;
                    Debug.Log($"🎯 Drone {droneID}: Formation position reached");
                    break;
            }
        }
    }
    
    /// <summary>
    /// Akıllı thrust hesaplama - durum bazlı
    /// </summary>
    float CalculateIntelligentThrust()
    {
        float currentHeight = transform.position.y;
        float thrust = hoverThrust;
        
        switch (currentState)
        {
            case DroneState.TakingOff:
                if (currentHeight < targetHeight - 0.5f)
                {
                    thrust = thrustForce * 1.2f;
                }
                else
                {
                    currentState = DroneState.Hovering;
                    Debug.Log($"🚁 Drone {droneID}: Takeoff complete at {currentHeight:F1}m");
                }
                break;
                
            case DroneState.Landing:
                thrust = hoverThrust * 0.3f;
                if (currentHeight <= 1.5f)
                {
                    currentState = DroneState.Grounded;
                    rb.linearVelocity = Vector3.zero;
                    Debug.Log($"🛬 Drone {droneID}: Landing complete");
                }
                break;
                
            case DroneState.FormationHold:
                // Hassas yükseklik kontrolü
                float heightError = assignedPosition.y - currentHeight;
                thrust = hoverThrust + (heightError * 5f);
                thrust = Mathf.Clamp(thrust, hoverThrust * 0.6f, hoverThrust * 1.8f);
                break;
                
            default:
                // Normal hover kontrolü
                float heightError2 = targetHeight - currentHeight;
                thrust = hoverThrust + (heightError2 * 3f);
                thrust = Mathf.Clamp(thrust, hoverThrust * 0.7f, hoverThrust * 1.4f);
                break;
        }
        
        return thrust;
    }
    
    /// <summary>
    /// Takılma durumu kontrolü
    /// </summary>
    void CheckStuckState()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < 0.1f && currentState != DroneState.Grounded && currentState != DroneState.FormationHold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_THRESHOLD)
            {
                Debug.LogWarning($"⚠️ Drone {droneID} stuck! Applying emergency thrust.");
                rb.AddForce(Vector3.up * thrustForce * 0.5f);
                rb.AddForce(Random.insideUnitSphere * moveForce * 0.3f);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        
        lastPosition = transform.position;
    }
    
    // =================================================================
    // PUBLIC INTERFACE METHODS
    // =================================================================
    
    /// <summary>
    /// Drone'u arm et (hazır duruma getir)
    /// </summary>
    public void ArmDrone() 
    { 
        currentState = DroneState.Armed;
        Debug.Log($"🔧 Drone {droneID}: Armed and ready");
    }
    
    /// <summary>
    /// Akıllı kalkış - hedef yüksekliğe
    /// </summary>
    public void SmartTakeOff(float height)
    {
        if (currentState != DroneState.Armed) 
        {
            Debug.LogWarning($"⚠️ Drone {droneID}: Cannot takeoff - not armed!");
            return;
        }
        
        targetHeight = height;
        currentState = DroneState.TakingOff;
        Debug.Log($"🚀 Drone {droneID}: Takeoff to {height}m");
    }
    
    /// <summary>
    /// Staging pozisyonu belirle
    /// </summary>
    public void SetStagingPosition(Vector3 position) 
    { 
        stagingPosition = position;
        Debug.Log($"🎯 Drone {droneID}: Staging position set to {position}");
    }
    
    /// <summary>
    /// Staging pozisyonuna hareket et
    /// </summary>
    public void MoveToStaging()
    {
        if (currentState == DroneState.Grounded) 
        {
            Debug.LogWarning($"⚠️ Drone {droneID}: Cannot move to staging - grounded!");
            return;
        }
        
        currentState = DroneState.Staging;
        targetHeight = stagingPosition.y;
        Debug.Log($"🎯 Drone {droneID}: Moving to staging");
    }
    
    /// <summary>
    /// Formasyon pozisyonu belirle
    /// </summary>
    public void SetAssignedPosition(Vector3 position) 
    { 
        assignedPosition = position;
        Debug.Log($"📍 Drone {droneID}: Formation position assigned {position}");
    }
    
    /// <summary>
    /// Formasyon hareketine başla
    /// </summary>
    public void StartSmartFormationMove()
    {
        if (currentState == DroneState.Grounded) 
        {
            Debug.LogWarning($"⚠️ Drone {droneID}: Cannot start formation move - grounded!");
            return;
        }
        
        currentState = DroneState.FormationMove;
        Debug.Log($"🎯 Drone {droneID}: Starting formation move");
    }
    
    /// <summary>
    /// Hızlı formasyon hareketi
    /// </summary>
    public void StartFastFormationMove()
    {
        if (currentState == DroneState.Grounded) return;
        
        currentState = DroneState.FastFormationMove;
        Debug.Log($"⚡ Drone {droneID}: Starting fast formation move");
    }
    
    /// <summary>
    /// Formasyonu kilitle - hassas pozisyon tutma
    /// </summary>
    public void LockFormation() 
    { 
        currentState = DroneState.FormationHold;
        Debug.Log($"🔒 Drone {droneID}: Formation locked");
    }
    
    /// <summary>
    /// Formasyon kilidini aç
    /// </summary>
    public void UnlockFormation() 
    { 
        currentState = DroneState.Hovering;
        Debug.Log($"🔓 Drone {droneID}: Formation unlocked");
    }
    
    /// <summary>
    /// İniş başlat
    /// </summary>
    public void Land()
    {
        currentState = DroneState.Landing;
        targetHeight = 1f;
        Debug.Log($"🛬 Drone {droneID}: Landing initiated");
    }
    
    // =================================================================
    // GETTERS - Veri erişimi
    // =================================================================
    
    public Vector3 GetVelocity() 
    { 
        return rb != null ? rb.linearVelocity : Vector3.zero; 
    }
    
    public Vector3 GetTargetPosition()
    {
        switch (currentState)
        {
            case DroneState.Staging: return stagingPosition;
            case DroneState.FormationMove:
            case DroneState.FormationHold: return assignedPosition;
            default: return transform.position;
        }
    }
    
    public DroneState GetCurrentState()
    {
        return currentState;
    }
    
    public bool IsGrounded()
    {
        return currentState == DroneState.Grounded;
    }
    
    public bool IsInFormation()
    {
        return currentState == DroneState.FormationHold;
    }
    
    public float GetDistanceToTarget()
    {
        Vector3 target = GetTargetPosition();
        return Vector3.Distance(transform.position, target);
    }
    
    // =================================================================
    // DEBUG VE GIZMOS
    // =================================================================
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Drone ID göster
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"D{droneID}");
        
        // Target pozisyonu göster
        Vector3 target = GetTargetPosition();
        if (target != transform.position)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target, 0.5f);
            Gizmos.DrawLine(transform.position, target);
        }
        
        // Durum rengi
        switch (currentState)
        {
            case DroneState.Grounded: Gizmos.color = Color.red; break;
            case DroneState.TakingOff: Gizmos.color = Color.blue; break;
            case DroneState.Hovering: Gizmos.color = Color.green; break;
            case DroneState.FormationMove: Gizmos.color = Color.cyan; break;
            case DroneState.FormationHold: Gizmos.color = Color.magenta; break;
            case DroneState.Landing: Gizmos.color = Color.orange; break;
        }
        
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}