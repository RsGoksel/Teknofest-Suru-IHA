// =================================================================
// DİNAMİK FORMASYON HESAPLAMA SİSTEMİ
// =================================================================
// AMAÇ: Farklı sayıdaki İHA'lar için matematiksel formüller ile
//       otomatik formasyon pozisyonları hesaplama
// =================================================================

public class DynamicFormationGenerator
{
    private float baseAltitude;          // Temel uçuş irtifası
    private float agentDistance;         // İHA'lar arası mesafe
    private int totalDroneCount;         // Toplam İHA sayısı
    
    public DynamicFormationGenerator(float altitude, float distance, int droneCount)
    {
        baseAltitude = altitude;
        agentDistance = distance;
        totalDroneCount = droneCount;
    }
    
    /// <summary>
    /// DİNAMİK V FORMASYONU HESAPLAMA
    /// Matematiksel Model: V şekli için sol ve sağ kanat dağılımı
    /// 
    /// V Formasyonu Geometrisi:
    /// - Alt merkez nokta: (0, h_base, 0)
    /// - Sol kanat: (-d*i, h_base + k*i, 0) for i = 1,2,3...
    /// - Sağ kanat: (+d*i, h_base + k*i, 0) for i = 1,2,3...
    /// 
    /// Burada:
    /// d = agentDistance (yatay mesafe)
    /// k = heightIncrement (dikey mesafe artışı)
    /// h_base = baseAltitude (temel irtifa)
    /// </summary>
    public Vector3[] GenerateDynamicVFormation()
    {
        Vector3[] positions = new Vector3[totalDroneCount];
        
        // 1. V'NİN ALT MERKEZ NOKTASI (İlk İHA)
        positions[0] = new Vector3(0, baseAltitude, 0);
        
        if (totalDroneCount == 1) return positions;
        
        // 2. KANAT DAĞILIMI HESAPLAMA
        int remainingDrones = totalDroneCount - 1;  // Merkez hariç
        int leftWingCount = remainingDrones / 2;    // Sol kanat İHA sayısı
        int rightWingCount = remainingDrones - leftWingCount; // Sağ kanat İHA sayısı
        
        // 3. SOL KANAT POZİSYONLARI
        // Matematiksel formül: P_left(i) = (-d*i*0.8, h_base + 2.5*i, 0)
        for (int i = 0; i < leftWingCount; i++)
        {
            float wingIndex = i + 1; // 1'den başla
            positions[i + 1] = new Vector3(
                -agentDistance * wingIndex * 0.8f,           // X: Sol tarafa doğru
                baseAltitude + (wingIndex * 2.5f),           // Y: Yukarı doğru
                0f                                           // Z: Sabit
            );
            
            // DEBUG: Pozisyon bilgisi
            Debug.Log($"Sol Kanat İHA {wingIndex}: X={positions[i + 1].x:F1}, " +
                     $"Y={positions[i + 1].y:F1}, Z={positions[i + 1].z:F1}");
        }
        
        // 4. SAĞ KANAT POZİSYONLARI  
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
            
            // DEBUG: Pozisyon bilgisi
            Debug.Log($"Sağ Kanat İHA {wingIndex}: X={positions[arrayIndex].x:F1}, " +
                     $"Y={positions[arrayIndex].y:F1}, Z={positions[arrayIndex].z:F1}");
        }
        
        Debug.Log($"✅ V Formasyonu: {totalDroneCount} İHA, Sol Kanat: {leftWingCount}, " +
                 $"Sağ Kanat: {rightWingCount}");
        
        return positions;
    }
    
    /// <summary>
    /// DİNAMİK OK FORMASYONU HESAPLAMA
    /// Matematiksel Model: Ok şekli için uç, kanatlar ve kuyruk dağılımı
    /// 
    /// Ok Formasyonu Geometrisi:
    /// - Uç nokta: (0, h_max, 0)
    /// - Sol kanat: (-d*i, h_max - k*i, -z*i) for i = 1,2,3...
    /// - Sağ kanat: (+d*i, h_max - k*i, -z*i) for i = 1,2,3...
    /// - Kuyruk: (0, h_min, -z_max)
    /// </summary>
    public Vector3[] GenerateDynamicArrowFormation()
    {
        Vector3[] positions = new Vector3[totalDroneCount];
        
        // 1. OK BAŞI (UÇ NOKTA)
        positions[0] = new Vector3(0, baseAltitude + 6, 0);
        
        if (totalDroneCount == 1) return positions;
        
        // 2. KUYRUK POZİSYONU (Son İHA)
        if (totalDroneCount > 1)
        {
            positions[totalDroneCount - 1] = new Vector3(
                0, 
                baseAltitude - 4, 
                -(totalDroneCount * 2)  // Derinlik İHA sayısına göre
            );
        }
        
        // 3. KANAT DAĞILIMI
        int sideCount = totalDroneCount - 2;  // Uç ve kuyruk hariç
        int leftWingCount = sideCount / 2;
        int rightWingCount = sideCount - leftWingCount;
        
        // 4. SOL KANAT HESAPLAMA
        // Matematiksel formül: P_left(i) = (-d*i, h_base + 4 - step*8, -step*depth)
        for (int i = 0; i < leftWingCount; i++)
        {
            float wingStep = (float)(i + 1) / (leftWingCount + 1);  // Normalize step
            positions[i + 1] = new Vector3(
                -agentDistance * (i + 1),                           // X: Sol tarafa
                baseAltitude + 4 - (wingStep * 8),                  // Y: Alçalarak
                -(wingStep * totalDroneCount * 1.5f)                // Z: Geriye doğru
            );
        }
        
        // 5. SAĞ KANAT HESAPLAMA
        for (int i = 0; i < rightWingCount; i++)
        {
            float wingStep = (float)(i + 1) / (rightWingCount + 1);
            int arrayIndex = leftWingCount + i + 1;
            
            positions[arrayIndex] = new Vector3(
                agentDistance * (i + 1),                            // X: Sağ tarafa
                baseAltitude + 4 - (wingStep * 8),                  // Y: Alçalarak
                -(wingStep * totalDroneCount * 1.5f)                // Z: Geriye doğru
            );
        }
        
        Debug.Log($"✅ Ok Formasyonu: {totalDroneCount} İHA, Uç+Kuyruk: 2, " +
                 $"Sol Kanat: {leftWingCount}, Sağ Kanat: {rightWingCount}");
        
        return positions;
    }
    
    /// <summary>
    /// DİNAMİK ÇEMBER STAGING HESAPLAMA
    /// Amaç: Formasyona geçmeden önce İHA'ları dairesel olarak konumlandır
    /// 
    /// Matematiksel Model: Çember üzerinde eşit açılı nokta dağılımı
    /// P(i) = (R*cos(θ_i), h, R*sin(θ_i))
    /// Burada θ_i = (360° / N) * i, N = toplam İHA sayısı
    /// </summary>
    public Vector3[] GenerateCircularStaging(Vector3 center, float radius)
    {
        Vector3[] stagingPositions = new Vector3[totalDroneCount];
        
        // 1. AÇISAL ADIM HESAPLAMA
        // θ_step = 360° / N
        float angleStep = 360f / (float)totalDroneCount;
        
        Debug.Log($"🎯 Çember Staging: {totalDroneCount} İHA, Yarıçap: {radius}m, " +
                 $"Açısal Adım: {angleStep:F1}°");
        
        // 2. HER İHA İÇİN ÇEMBER ÜZERİNDE POZİSYON HESAPLAMA
        for (int i = 0; i < totalDroneCount; i++)
        {
            // Açıyı radyana çevir: θ_rad = θ_degree * π/180
            float angleInRadians = i * angleStep * Mathf.Deg2Rad;
            
            // Çember denklemi: x = R*cos(θ), z = R*sin(θ)
            stagingPositions[i] = new Vector3(
                center.x + radius * Mathf.Cos(angleInRadians),     // X koordinatı
                center.y,                                          // Y sabit
                center.z + radius * Mathf.Sin(angleInRadians)      // Z koordinatı
            );
            
            // DEBUG: Her İHA'nın açısı ve pozisyonu
            Debug.Log($"İHA {i+1}: Açı={i * angleStep:F1}°, " +
                     $"Pos=({stagingPositions[i].x:F1}, {stagingPositions[i].y:F1}, " +
                     $"{stagingPositions[i].z:F1})");
        }
        
        return stagingPositions;
    }
    
    /// <summary>
    /// FORMASYON MERKEZİ HESAPLAMA
    /// Matematiksel Model: Ağırlık merkezi hesaplama
    /// P_center = (1/N) * Σ P_i
    /// </summary>
    public Vector3 CalculateFormationCenter(Vector3[] positions)
    {
        Vector3 center = Vector3.zero;
        
        // Tüm pozisyonları topla
        for (int i = 0; i < positions.Length; i++)
        {
            center += positions[i];
        }
        
        // Ortalama al (ağırlık merkezi)
        center /= positions.Length;
        
        Debug.Log($"📐 Formasyon Merkezi: ({center.x:F1}, {center.y:F1}, {center.z:F1})");
        
        return center;
    }
}