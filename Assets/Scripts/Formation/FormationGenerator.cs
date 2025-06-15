using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TEKNOFEST Sürü İHA - Dinamik Formasyon Hesaplama Sistemi
/// Matematiksel formüller ile otomatik formasyon pozisyonları
/// </summary>
public class FormationGenerator : MonoBehaviour
{
    [Header("Formation Parameters")]
    public bool enableDebugGizmos = true;
    public Color gizmosColor = Color.yellow;
    
    // Cached formation data
    private Vector3[] lastGeneratedPositions;
    private FormationType lastFormationType = FormationType.None;
    private int lastDroneCount = 0;
    
    /// <summary>
    /// Dinamik V Formasyonu Hesaplama
    /// Matematiksel Model: V şekli için sol ve sağ kanat dağılımı
    /// 
    /// V Formasyonu Geometrisi:
    /// - Alt merkez nokta: (0, h_base, 0)
    /// - Sol kanat: (-d*i*0.8, h_base + k*i, 0) for i = 1,2,3...
    /// - Sağ kanat: (+d*i*0.8, h_base + k*i, 0) for i = 1,2,3...
    /// </summary>
    public Vector3[] GenerateDynamicVFormation(int droneCount, float baseAltitude, float agentDistance)
    {
        Vector3[] positions = new Vector3[droneCount];
        
        // 1. V'nin alt merkez noktası (İlk drone)
        positions[0] = new Vector3(0, baseAltitude, 0);
        
        if (droneCount == 1) 
        {
            lastGeneratedPositions = positions;
            lastFormationType = FormationType.VFormation;
            lastDroneCount = droneCount;
            return positions;
        }
        
        // 2. Kanat dağılımı hesaplama
        int remainingDrones = droneCount - 1;  // Merkez hariç
        int leftWingCount = remainingDrones / 2;    // Sol kanat drone sayısı
        int rightWingCount = remainingDrones - leftWingCount; // Sağ kanat drone sayısı
        
        Debug.Log($"📐 V Formation: Total={droneCount}, Left={leftWingCount}, Right={rightWingCount}");
        
        // 3. Sol kanat pozisyonları
        // Matematiksel formül: P_left(i) = (-d*i*0.8, h_base + 2.5*i, 0)
        for (int i = 0; i < leftWingCount; i++)
        {
            float wingIndex = i + 1; // 1'den başla
            positions[i + 1] = new Vector3(
                -agentDistance * wingIndex * 0.8f,           // X: Sol tarafa doğru
                baseAltitude + (wingIndex * 2.5f),           // Y: Yukarı doğru
                0f                                           // Z: Sabit
            );
        }
        
        // 4. Sağ kanat pozisyonları  
        // Matematiksel formül: P_right(i) = (+d*i*0.8, h_base + 2.5*i, 0)
        for (int i = 0; i < rightWingCount; i++)
        {
            float wingIndex = i + 1; // 1'den başla
            int arrayIndex = leftWingCount + i + 1;
            
            positions[arrayIndex] = new Vector3(
                agentDistance * wingIndex * 0.8f,            // X: Sağ tarafa doğru
                baseAltitude + (wingIndex * 2.5f),           // Y: Yukarı doğru
                0f                                           // Z: Sabit
            );
        }
        
        lastGeneratedPositions = positions;
        lastFormationType = FormationType.VFormation;
        lastDroneCount = droneCount;
        
        Debug.Log($"✅ V Formation generated: {droneCount} drones");
        return positions;
    }
    
    /// <summary>
    /// Dinamik Ok (Arrow) Formasyonu Hesaplama
    /// Matematiksel Model: Ok şekli için uç, kanatlar ve kuyruk dağılımı
    /// 
    /// Ok Formasyonu Geometrisi:
    /// - Uç nokta: (0, h_max, 0)
    /// - Sol kanat: (-d*i, h_max - k*i, -z*i) for i = 1,2,3...
    /// - Sağ kanat: (+d*i, h_max - k*i, -z*i) for i = 1,2,3...
    /// - Kuyruk: (0, h_min, -z_max)
    /// </summary>
    public Vector3[] GenerateDynamicArrowFormation(int droneCount, float baseAltitude, float agentDistance)
    {
        Vector3[] positions = new Vector3[droneCount];
        
        // 1. Ok başı (uç nokta)
        positions[0] = new Vector3(0, baseAltitude + 6, 0);
        
        if (droneCount == 1) 
        {
            lastGeneratedPositions = positions;
            lastFormationType = FormationType.ArrowFormation;
            return positions;
        }
        
        // 2. Kuyruk pozisyonu (son drone)
        if (droneCount > 1)
        {
            positions[droneCount - 1] = new Vector3(
                0, 
                baseAltitude - 4, 
                -(droneCount * 2)  // Derinlik drone sayısına göre
            );
        }
        
        // 3. Kanat dağılımı
        int sideCount = droneCount - 2;  // Uç ve kuyruk hariç
        int leftWingCount = sideCount / 2;
        int rightWingCount = sideCount - leftWingCount;
        
        Debug.Log($"🏹 Arrow Formation: Total={droneCount}, Tip+Tail=2, Left={leftWingCount}, Right={rightWingCount}");
        
        // 4. Sol kanat hesaplama
        // Matematiksel formül: P_left(i) = (-d*i, h_base + 4 - step*8, -step*depth)
        for (int i = 0; i < leftWingCount; i++)
        {
            float wingStep = (float)(i + 1) / (leftWingCount + 1);  // Normalize step
            positions[i + 1] = new Vector3(
                -agentDistance * (i + 1),                           // X: Sol tarafa
                baseAltitude + 4 - (wingStep * 8),                  // Y: Alçalarak
                -(wingStep * droneCount * 1.5f)                     // Z: Geriye doğru
            );
        }
        
        // 5. Sağ kanat hesaplama
        for (int i = 0; i < rightWingCount; i++)
        {
            float wingStep = (float)(i + 1) / (rightWingCount + 1);
            int arrayIndex = leftWingCount + i + 1;
            
            positions[arrayIndex] = new Vector3(
                agentDistance * (i + 1),                            // X: Sağ tarafa
                baseAltitude + 4 - (wingStep * 8),                  // Y: Alçalarak
                -(wingStep * droneCount * 1.5f)                     // Z: Geriye doğru
            );
        }
        
        lastGeneratedPositions = positions;
        lastFormationType = FormationType.ArrowFormation;
        lastDroneCount = droneCount;
        
        Debug.Log($"✅ Arrow Formation generated: {droneCount} drones");
        return positions;
    }
    
    /// <summary>
    /// Çizgi (Line) Formasyonu Hesaplama
    /// </summary>
    public Vector3[] GenerateLineFormation(int droneCount, float baseAltitude, float agentDistance)
    {
        Vector3[] positions = new Vector3[droneCount];
        
        // Toplam genişlik hesaplama
        float totalWidth = (droneCount - 1) * agentDistance;
        float startX = -totalWidth / 2f;
        
        Debug.Log($"➖ Line Formation: {droneCount} drones, Width={totalWidth:F1}m");
        
        for (int i = 0; i < droneCount; i++)
        {
            positions[i] = new Vector3(
                startX + (i * agentDistance),   // X: Eşit aralıklı
                baseAltitude,                   // Y: Sabit irtifa
                0f                              // Z: Sabit
            );
        }
        
        lastGeneratedPositions = positions;
        lastFormationType = FormationType.LineFormation;
        lastDroneCount = droneCount;
        
        Debug.Log($"✅ Line Formation generated: {droneCount} drones");
        return positions;
    }
    
    /// <summary>
    /// Dikey (Vertical) Formasyon Hesaplama
    /// </summary>
    public Vector3[] GenerateVerticalFormation(int droneCount, float baseAltitude, float agentDistance)
    {
        Vector3[] positions = new Vector3[droneCount];
        
        Debug.Log($"📏 Vertical Formation: {droneCount} drones");
        
        for (int i = 0; i < droneCount; i++)
        {
            positions[i] = new Vector3(
                0f,                                         // X: Sabit
                baseAltitude + (i * agentDistance * 0.6f),  // Y: Yukarı doğru
                0f                                          // Z: Sabit
            );
        }
        
        lastGeneratedPositions = positions;
        lastFormationType = FormationType.VerticalFormation;
        lastDroneCount = droneCount;
        
        Debug.Log($"✅ Vertical Formation generated: {droneCount} drones");
        return positions;
    }
    
    /// <summary>
    /// Dairesel Staging Hesaplama
    /// Amaç: Formasyon öncesi drone'ları dairesel olarak konumlandır
    /// 
    /// Matematiksel Model: Çember üzerinde eşit açılı nokta dağılımı
    /// P(i) = (R*cos(θ_i), h, R*sin(θ_i))
    /// Burada θ_i = (360° / N) * i, N = toplam drone sayısı
    /// </summary>
    public Vector3[] GenerateCircularStaging(Vector3[] formationPositions, int droneCount, float radius)
    {
        Vector3[] stagingPositions = new Vector3[droneCount];
        
        // Formasyon merkezini hesapla
        Vector3 center = CalculateFormationCenter(formationPositions);
        
        // Açısal adım hesaplama
        float angleStep = 360f / (float)droneCount;
        
        Debug.Log($"🎯 Circular Staging: {droneCount} drones, Radius={radius}m, Center={center}");
        
        // Her drone için çember üzerinde pozisyon hesaplama
        for (int i = 0; i < droneCount; i++)
        {
            // Açıyı radyana çevir
            float angleInRadians = i * angleStep * Mathf.Deg2Rad;
            
            // Çember denklemi
            stagingPositions[i] = new Vector3(
                center.x + radius * Mathf.Cos(angleInRadians),     // X koordinatı
                center.y,                                          // Y sabit
                center.z + radius * Mathf.Sin(angleInRadians)      // Z koordinatı
            );
        }
        
        return stagingPositions;
    }
    
    /// <summary>
    /// Formasyon merkezi hesaplama
    /// Matematiksel Model: Ağırlık merkezi hesaplama
    /// P_center = (1/N) * Σ P_i
    /// </summary>
    public Vector3 CalculateFormationCenter(Vector3[] positions)
    {
        if (positions.Length == 0) return Vector3.zero;
        
        Vector3 center = Vector3.zero;
        
        // Tüm pozisyonları topla
        for (int i = 0; i < positions.Length; i++)
        {
            center += positions[i];
        }
        
        // Ortalama al (ağırlık merkezi)
        center /= positions.Length;
        
        return center;
    }
    
    /// <summary>
    /// Formasyon kalitesi analizi
    /// </summary>
    public float AnalyzeFormationQuality(Vector3[] actualPositions, Vector3[] targetPositions)
    {
        if (actualPositions.Length != targetPositions.Length || actualPositions.Length == 0)
            return 0f;
        
        float totalError = 0f;
        float maxAllowedError = 2f; // 2 metre maksimum hata
        
        for (int i = 0; i < actualPositions.Length; i++)
        {
            float error = Vector3.Distance(actualPositions[i], targetPositions[i]);
            totalError += Mathf.Min(error, maxAllowedError);
        }
        
        float averageError = totalError / actualPositions.Length;
        float quality = Mathf.Clamp01(1f - (averageError / maxAllowedError));
        
        return quality * 100f; // Yüzde olarak döndür
    }
    
    /// <summary>
    /// Formasyon validasyonu
    /// </summary>
    public bool ValidateFormation(Vector3[] positions, float minDistance)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            for (int j = i + 1; j < positions.Length; j++)
            {
                float distance = Vector3.Distance(positions[i], positions[j]);
                if (distance < minDistance)
                {
                    Debug.LogWarning($"⚠️ Formation validation failed: Drones {i} and {j} too close ({distance:F2}m < {minDistance}m)");
                    return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Özel formasyon oluşturma - custom pattern
    /// </summary>
    public Vector3[] GenerateCustomFormation(Vector3[] customPoints, int droneCount, float baseAltitude)
    {
        if (customPoints.Length == 0) 
        {
            Debug.LogWarning("⚠️ No custom points provided, generating line formation");
            return GenerateLineFormation(droneCount, baseAltitude, 5f);
        }
        
        Vector3[] positions = new Vector3[droneCount];
        
        // Drone sayısı custom point sayısından fazlaysa interpolate et
        if (droneCount > customPoints.Length)
        {
            for (int i = 0; i < droneCount; i++)
            {
                float t = (float)i / (droneCount - 1);
                int pointIndex = Mathf.FloorToInt(t * (customPoints.Length - 1));
                pointIndex = Mathf.Clamp(pointIndex, 0, customPoints.Length - 1);
                
                Vector3 point = customPoints[pointIndex];
                point.y = baseAltitude; // Irtifayı ayarla
                positions[i] = point;
            }
        }
        else
        {
            // Drone sayısı az ise direkt ata
            for (int i = 0; i < droneCount; i++)
            {
                Vector3 point = customPoints[i % customPoints.Length];
                point.y = baseAltitude;
                positions[i] = point;
            }
        }
        
        lastGeneratedPositions = positions;
        lastFormationType = FormationType.CustomFormation;
        lastDroneCount = droneCount;
        
        Debug.Log($"✅ Custom Formation generated: {droneCount} drones from {customPoints.Length} points");
        return positions;
    }
    
    // =================================================================
    // UTILITY METHODS
    // =================================================================
    
    public Vector3[] GetLastGeneratedPositions() => lastGeneratedPositions;
    public FormationType GetLastFormationType() => lastFormationType;
    
    /// <summary>
    /// Formasyon morph - bir formasyondan diğerine geçiş
    /// </summary>
    public Vector3[] MorphFormation(Vector3[] fromPositions, Vector3[] toPositions, float t)
    {
        if (fromPositions.Length != toPositions.Length)
        {
            Debug.LogWarning("⚠️ Formation morph: Position array lengths don't match");
            return toPositions;
        }
        
        Vector3[] morphedPositions = new Vector3[fromPositions.Length];
        
        for (int i = 0; i < fromPositions.Length; i++)
        {
            morphedPositions[i] = Vector3.Lerp(fromPositions[i], toPositions[i], t);
        }
        
        return morphedPositions;
    }
    
    /// <summary>
    /// Formasyon boyutlandırma - scale adjustment
    /// </summary>
    public Vector3[] ScaleFormation(Vector3[] positions, float scale, Vector3 center)
    {
        Vector3[] scaledPositions = new Vector3[positions.Length];
        
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 offset = positions[i] - center;
            scaledPositions[i] = center + offset * scale;
        }
        
        return scaledPositions;
    }
    
    /// <summary>
    /// Formasyon rotasyonu
    /// </summary>
    public Vector3[] RotateFormation(Vector3[] positions, float angleDegrees, Vector3 center, Vector3 axis)
    {
        Vector3[] rotatedPositions = new Vector3[positions.Length];
        Quaternion rotation = Quaternion.AngleAxis(angleDegrees, axis);
        
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 offset = positions[i] - center;
            Vector3 rotatedOffset = rotation * offset;
            rotatedPositions[i] = center + rotatedOffset;
        }
        
        return rotatedPositions;
    }
    
    // =================================================================
    // DEBUG VE VISUALIZATION
    // =================================================================
    
    void OnDrawGizmos()
    {
        if (!enableDebugGizmos || lastGeneratedPositions == null) return;
        
        // Formation center
        Vector3 center = CalculateFormationCenter(lastGeneratedPositions);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, 1f);
        
        // Formation positions
        Gizmos.color = gizmosColor;
        for (int i = 0; i < lastGeneratedPositions.Length; i++)
        {
            Vector3 pos = lastGeneratedPositions[i];
            
            // Position sphere
            Gizmos.DrawWireSphere(pos, 0.5f);
            
            // Drone number
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up, i.ToString());
            #endif
            
            // Connection lines for formation visualization
            if (lastFormationType == FormationType.VFormation)
            {
                if (i > 0) // Connect to center (index 0)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(pos, lastGeneratedPositions[0]);
                }
            }
            else if (lastFormationType == FormationType.LineFormation)
            {
                if (i > 0) // Connect to previous drone
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(pos, lastGeneratedPositions[i - 1]);
                }
            }
        }
        
        // Formation info
        #if UNITY_EDITOR
        string formationInfo = $"Formation: {lastFormationType}\nDrones: {lastDroneCount}\nCenter: {center}";
        UnityEditor.Handles.Label(center + Vector3.up * 3, formationInfo);
        #endif
    }
    
    /// <summary>
    /// Debug formation bilgilerini logla
    /// </summary>
    [ContextMenu("Debug Formation Info")]
    public void DebugFormationInfo()
    {
        if (lastGeneratedPositions == null)
        {
            Debug.Log("❌ No formation generated yet");
            return;
        }
        
        Debug.Log($"📊 FORMATION DEBUG INFO");
        Debug.Log($"   Type: {lastFormationType}");
        Debug.Log($"   Drone Count: {lastDroneCount}");
        Debug.Log($"   Center: {CalculateFormationCenter(lastGeneratedPositions)}");
        
        for (int i = 0; i < lastGeneratedPositions.Length; i++)
        {
            Debug.Log($"   Drone {i}: {lastGeneratedPositions[i]}");
        }
    }
    
    /// <summary>
    /// Formation pattern'ını dosyaya kaydet
    /// </summary>
    public void SaveFormationPattern(string filename)
    {
        if (lastGeneratedPositions == null) return;
        
        FormationPattern pattern = new FormationPattern
        {
            formationType = lastFormationType,
            droneCount = lastDroneCount,
            positions = lastGeneratedPositions
        };
        
        string json = JsonUtility.ToJson(pattern, true);
        System.IO.File.WriteAllText(Application.dataPath + "/" + filename + ".json", json);
        
        Debug.Log($"💾 Formation pattern saved: {filename}.json");
    }
    
    /// <summary>
    /// Formation pattern'ını dosyadan yükle
    /// </summary>
    public Vector3[] LoadFormationPattern(string filename)
    {
        string path = Application.dataPath + "/" + filename + ".json";
        
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError($"❌ Formation file not found: {filename}.json");
            return null;
        }
        
        string json = System.IO.File.ReadAllText(path);
        FormationPattern pattern = JsonUtility.FromJson<FormationPattern>(json);
        
        lastGeneratedPositions = pattern.positions;
        lastFormationType = pattern.formationType;
        lastDroneCount = pattern.droneCount;
        
        Debug.Log($"📁 Formation pattern loaded: {filename}.json");
        return pattern.positions;
    }
}

/// <summary>
/// Formation Pattern serileştirme için yardımcı sınıf
/// </summary>
[System.Serializable]
public class FormationPattern
{
    public FormationType formationType;
    public int droneCount;
    public Vector3[] positions;
}