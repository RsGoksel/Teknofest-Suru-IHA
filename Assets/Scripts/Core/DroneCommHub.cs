using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TEKNOFEST Sürü İHA - Merkezi İletişim Hub'ı
/// TCP Handshake protokolü ve güvenli yol planlama
/// </summary>
public class DroneCommHub : MonoBehaviour
{
    [Header("Communication Parameters")]
    public float communicationRange = 20f;
    public float updateFrequency = 50f;
    public int maxConcurrentDrones = 50;
    
    // Hub State
    private DroneSpawner spawner;
    private bool isInitialized = false;
    
    // Performance Metrics
    private float lastUpdateTime;
    private int processedRequests = 0;
    private float averageResponseTime = 0f;
    
    public void Initialize(DroneSpawner droneSpawner)
    {
        spawner = droneSpawner;
        isInitialized = true;
        
        // Start communication loop
        InvokeRepeating(nameof(ProcessCommunications), 0f, 1f / updateFrequency);
        
        Debug.Log($"📡 Communication Hub initialized - Range: {communicationRange}m, Freq: {updateFrequency}Hz");
    }
    
    /// <summary>
    /// Ana güvenli yol hesaplama API
    /// TCP Handshake simülasyonu ile
    /// </summary>
    public Vector3 RequestSafePathVector(int droneID, Vector3 currentPos, Vector3 targetPos)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("📡 Communication Hub not initialized!");
            return (targetPos - currentPos).normalized;
        }
        
        float requestStartTime = Time.time;
        
        // 1. TCP HANDSHAKE SİMÜLASYONU
        if (!PerformTCPHandshake(droneID))
        {
            Debug.LogWarning($"📡 TCP Handshake failed for Drone {droneID}");
            return GetFallbackDirection(currentPos, targetPos);
        }
        
        // 2. YAKINDAKI DRONE'LARI SORGULA
        List<SmartDroneData> nearbyDrones = spawner.GetNearbyDroneData(droneID, currentPos, communicationRange);
        
        // 3. GÜVENLİ YOL HESAPLAMA
        Vector3 safePathVector = CalculateSafePathVector(currentPos, targetPos, nearbyDrones);
        
        // 4. PERFORMANS METRİĞİ GÜNCELLE
        float responseTime = Time.time - requestStartTime;
        UpdatePerformanceMetrics(responseTime);
        processedRequests++;
        
        return safePathVector;
    }
    
    /// <summary>
    /// TCP Handshake Protokolü Simülasyonu
    /// Gerçek TCP handshake mantığını taklit eder
    /// </summary>
    bool PerformTCPHandshake(int droneID)
    {
        // SYN - Synchronize
        if (!SendTCPMessage(droneID, "SYN"))
            return false;
        
        // SYN-ACK - Synchronize Acknowledge
        if (!ReceiveTCPMessage(droneID, "SYN-ACK"))
            return false;
        
        // ACK - Acknowledge
        if (!SendTCPMessage(droneID, "ACK"))
            return false;
        
        // Handshake başarılı
        return true;
    }
    
    /// <summary>
    /// TCP mesaj gönderme simülasyonu
    /// </summary>
    bool SendTCPMessage(int droneID, string messageType)
    {
        // İletişim menzili kontrolü
        if (spawner != null)
        {
            var droneData = spawner.GetNearbyDroneData(-1, transform.position, communicationRange);
            bool droneInRange = droneData.Exists(d => d.id == droneID);
            
            if (!droneInRange)
            {
                Debug.LogWarning($"📡 Drone {droneID} out of communication range for {messageType}");
                return false;
            }
        }
        
        // Simulated network delay
        float networkDelay = Random.Range(0.001f, 0.005f);
        
        // Packet loss simulation (1% chance)
        if (Random.Range(0f, 1f) < 0.01f)
        {
            Debug.LogWarning($"📡 Packet loss! {messageType} to Drone {droneID}");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// TCP mesaj alma simülasyonu
    /// </summary>
    bool ReceiveTCPMessage(int droneID, string expectedType)
    {
        // Network jitter simulation
        float jitter = Random.Range(0.001f, 0.003f);
        
        // Message corruption simulation (0.5% chance)
        if (Random.Range(0f, 1f) < 0.005f)
        {
            Debug.LogWarning($"📡 Message corruption! Expected {expectedType} from Drone {droneID}");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Gelişmiş güvenli yol hesaplama algoritması
    /// Potansiyel alan yöntemi + A* optimizasyonu
    /// </summary>
    Vector3 CalculateSafePathVector(Vector3 currentPos, Vector3 targetPos, List<SmartDroneData> obstacles)
    {
        // 1. HEDEF ÇEKİM KUVVETİ (Attractive Force)
        Vector3 attractiveForce = (targetPos - currentPos).normalized;
        float distance = Vector3.Distance(currentPos, targetPos);
        
        // 2. ÇARPIŞMA ÖNLEME KUVVETİ (Repulsive Force)
        Vector3 repulsiveForce = CalculateCollisionAvoidanceForce(currentPos, obstacles);
        
        // 3. DİNAMİK AĞIRLIK HESAPLAMA
        float dangerLevel = CalculateDangerLevel(currentPos, obstacles);
        float avoidanceWeight = Mathf.Clamp01(dangerLevel / 5f); // 5m max danger range
        float attractiveWeight = 1f - avoidanceWeight;
        
        // 4. A* HEURİSTİK OPTİMİZASYON
        Vector3 heuristicBias = CalculateAStarHeuristic(currentPos, targetPos, obstacles);
        
        // 5. TOPLAM GÜVENLİ YOL VEKTÖRÜ
        Vector3 safePathVector = (
            attractiveForce * attractiveWeight + 
            repulsiveForce.normalized * avoidanceWeight +
            heuristicBias * 0.1f
        ).normalized;
        
        // DEBUG: Kritik durumlar
        if (dangerLevel > 3f)
        {
            Debug.LogWarning($"🚨 High danger level: {dangerLevel:F2} at {currentPos}");
        }
        
        return safePathVector;
    }
    
    /// <summary>
    /// Çarpışma önleme kuvvet hesaplama
    /// Formül: F_avoid = Σ(K * (R_safe - d_i) / R_safe * û_i)
    /// </summary>
    Vector3 CalculateCollisionAvoidanceForce(Vector3 myPos, List<SmartDroneData> obstacles)
    {
        Vector3 totalAvoidanceForce = Vector3.zero;
        const float K = 3f; // Kuvvet sabiti
        const float R_safe = 4f; // Güvenlik yarıçapı
        
        foreach (SmartDroneData obstacle in obstacles)
        {
            float distance = obstacle.distance;
            
            if (distance < R_safe && distance > 0.1f)
            {
                // Gelecek pozisyon tahmini (Predictive Algorithm)
                Vector3 predictedPos = obstacle.position + obstacle.velocity * 2f;
                
                // Kaçınma yön vektörü
                Vector3 avoidanceDirection = (myPos - predictedPos).normalized;
                
                // Kuvvet büyüklüğü hesaplama
                float forceMagnitude = K * (R_safe - distance) / R_safe;
                
                totalAvoidanceForce += avoidanceDirection * forceMagnitude;
            }
        }
        
        return totalAvoidanceForce;
    }
    
    /// <summary>
    /// A* Heuristik optimizasyon
    /// ArUco marker keşif görevi için optimize edilmiş
    /// </summary>
    Vector3 CalculateAStarHeuristic(Vector3 current, Vector3 target, List<SmartDroneData> obstacles)
    {
        // Manhattan distance heuristic
        Vector3 direction = target - current;
        float manhattanDistance = Mathf.Abs(direction.x) + Mathf.Abs(direction.y) + Mathf.Abs(direction.z);
        
        // Obstacle density factor
        float obstacleDensity = obstacles.Count / 10f; // Normalize by expected max
        
        // Heuristic bias towards less crowded areas
        Vector3 heuristicVector = Vector3.zero;
        
        if (obstacleDensity > 0.5f) // High density area
        {
            // Find the direction with least obstacles
            Vector3[] directions = {
                Vector3.right, Vector3.left, Vector3.forward, Vector3.back
            };
            
            float minObstacles = float.MaxValue;
            Vector3 bestDirection = Vector3.zero;
            
            foreach (Vector3 dir in directions)
            {
                Vector3 testPos = current + dir * 3f;
                int nearbyCount = 0;
                
                foreach (var obstacle in obstacles)
                {
                    if (Vector3.Distance(testPos, obstacle.position) < 2f)
                        nearbyCount++;
                }
                
                if (nearbyCount < minObstacles)
                {
                    minObstacles = nearbyCount;
                    bestDirection = dir;
                }
            }
            
            heuristicVector = bestDirection * 0.5f;
        }
        
        return heuristicVector;
    }
    
    /// <summary>
    /// Tehlike seviyesi hesaplama
    /// </summary>
    float CalculateDangerLevel(Vector3 position, List<SmartDroneData> obstacles)
    {
        float totalDanger = 0f;
        
        foreach (SmartDroneData obstacle in obstacles)
        {
            float distance = obstacle.distance;
            if (distance < 5f) // 5m danger zone
            {
                // Distance-based danger (closer = more dangerous)
                float distanceDanger = (5f - distance) / 5f;
                
                // Speed-based danger (faster = more dangerous)
                float speedDanger = obstacle.velocity.magnitude / 10f; // Normalize by max expected speed
                
                totalDanger += distanceDanger + speedDanger * 0.3f;
            }
        }
        
        return totalDanger;
    }
    
    /// <summary>
    /// Fallback direction - haberleşme başarısız olduğunda
    /// </summary>
    Vector3 GetFallbackDirection(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 basicDirection = (targetPos - currentPos).normalized;
        
        // Add some randomness to avoid multiple drones taking same path
        Vector3 randomOffset = Random.insideUnitSphere * 0.2f;
        randomOffset.y = 0; // Keep horizontal
        
        return (basicDirection + randomOffset).normalized;
    }
    
    /// <summary>
    /// İletişim döngüsü - sistem durumu izleme
    /// </summary>
    void ProcessCommunications()
    {
        if (!isInitialized) return;
        
        // Hub sağlık kontrolü
        CheckHubHealth();
        
        // Performans metrikleri güncelle
        if (Time.time - lastUpdateTime >= 1f)
        {
            LogPerformanceMetrics();
            ResetPerformanceCounters();
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// Hub sağlık kontrolü
    /// </summary>
    void CheckHubHealth()
    {
        if (spawner == null)
        {
            Debug.LogError("📡 Communication Hub: Spawner reference lost!");
            return;
        }
        
        // Memory usage check (simulated)
        if (processedRequests > 1000)
        {
            Debug.LogWarning("📡 High communication load detected");
        }
    }
    
    /// <summary>
    /// Performans metriklerini güncelle
    /// </summary>
    void UpdatePerformanceMetrics(float responseTime)
    {
        if (averageResponseTime == 0f)
            averageResponseTime = responseTime;
        else
            averageResponseTime = (averageResponseTime + responseTime) / 2f;
    }
    
    /// <summary>
    /// Performans metriklerini logla
    /// </summary>
    void LogPerformanceMetrics()
    {
        if (processedRequests > 0)
        {
            Debug.Log($"📊 Communication Hub Performance:");
            Debug.Log($"   Requests/sec: {processedRequests}");
            Debug.Log($"   Avg Response Time: {averageResponseTime * 1000:F2}ms");
            Debug.Log($"   Communication Range: {communicationRange}m");
        }
    }
    
    /// <summary>
    /// Performans sayaçlarını sıfırla
    /// </summary>
    void ResetPerformanceCounters()
    {
        processedRequests = 0;
        averageResponseTime = 0f;
    }
    
    // =================================================================
    // PUBLIC API METHODS
    // =================================================================
    
    public void SetCommunicationRange(float range)
    {
        communicationRange = Mathf.Clamp(range, 5f, 50f);
        Debug.Log($"📡 Communication range updated: {communicationRange}m");
    }
    
    public void SetUpdateFrequency(float frequency)
    {
        updateFrequency = Mathf.Clamp(frequency, 10f, 100f);
        CancelInvoke(nameof(ProcessCommunications));
        InvokeRepeating(nameof(ProcessCommunications), 0f, 1f / updateFrequency);
        Debug.Log($"📡 Update frequency changed: {updateFrequency}Hz");
    }
    
    public bool IsHubHealthy()
    {
        return isInitialized && spawner != null;
    }
    
    public int GetProcessedRequests()
    {
        return processedRequests;
    }
    
    public float GetAverageResponseTime()
    {
        return averageResponseTime;
    }
    
    // =================================================================
    // DEBUG VE GIZMOS
    // =================================================================
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Communication range göster
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, communicationRange);
        
        // Hub durumu
        Gizmos.color = isInitialized ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
        
        // Hub bilgisi
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3, 
            $"CommHub\nRange: {communicationRange:F0}m\nReq/s: {processedRequests}");
    }
}