using UnityEngine;
using System.Collections;

public class BalikMotoru : MonoBehaviour
{
    [HideInInspector] public BalikSuruYonetici suruYonetici;
    [HideInInspector] public int balikIndex;
    [HideInInspector] public bool yemModu = false;

    [Header("Hız Ayarları")]
    [Tooltip("0 yaparsanız balık 1.0 ile 2.5 arası rastgele doğal bir hızda yüzer.")]
    public float hiz = 0f;
    public float turnRotateHizi = 3f;

    [Header("Turn Animasyonu")]
    public float keskinDonusAcisi = 90f; // Inspector'daki tok değerine eşitlendi
    public float turnHizYuzdesi = 0.01f; // Inspector'daki duraksama hissine eşitlendi
    private bool turnOynuyor = false;
    private float sonTurnZamani = -999f;
    private Vector3 turnBaslangicYonu;
    private Vector3 turnHedefYonu;
    private float turnSuresi = 0.5f;

    [Header("Engel Algılama")]
    public LayerMask engelKatmani;
    public float engelGormeMesafesi = 2.5f;
    public float engeldenKacisGucu = 8f;
    public float suSeviyesiUyariMesafesi = 0.3f;
    public float kacisGucu = 3f;

    [HideInInspector] public Vector3 hedefYon;
    [HideInInspector] public float aktifHizCarpani = 1f;

    private Rigidbody rb;
    private Animator animator;

    // 🎯 PERFORMANS İÇİN CACHE EDİLENLER (Saniyede 50 kere aranmaz, 1 kere bulunur)
    private SuruZekasi suruZekasi;
    private DipMekanigi dipMekanigi;
    private int duvarLayerMaskesi;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        suruZekasi = GetComponent<SuruZekasi>();
        dipMekanigi = GetComponent<DipMekanigi>();
        duvarLayerMaskesi = LayerMask.GetMask("Duvar");

        if (suruYonetici != null)
            hiz = Random.Range(suruYonetici.minHiz, suruYonetici.maxHiz);
        else if (hiz == 0f)
            hiz = Random.Range(1.0f, 2.5f); // O bahsettiğin harika detay burada işliyor

        if (animator != null)
            animator.CrossFade("Armature|Swim 1", 0.1f);

        hedefYon = Random.insideUnitSphere;
        hedefYon.y = 0;
        hedefYon.Normalize();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void FixedUpdate()
    {
        if (yemModu) return;

        Vector3 sonYon = hedefYon;
        aktifHizCarpani = 1f;
        Vector3 bakisYonu = hedefYon;

        if (!turnOynuyor)
        {
            Vector3 sinirKarari = SinirKontrolu();
            Vector3 engelKarari = EngelKontrolu();
            Vector3 rastgelelik = RastgeleYon();

            float merkezeDonus = suruYonetici != null ? suruYonetici.merkezeDonusGucu : 2f;
            Vector3 etkiYonu = (sinirKarari * merkezeDonus) + (rastgelelik * 0.3f) + (engelKarari * engeldenKacisGucu);

            // GetComponent çöpe atıldı, Cache'den çağırılıyor
            if (suruZekasi != null && suruYonetici != null)
                etkiYonu += suruZekasi.SuruYonuHesapla(this);

            if (etkiYonu.magnitude > 0.01f)
            {
                sonYon = etkiYonu;
            }

            sonYon.y *= 0.2f;

            if (suruYonetici != null && transform.position.y - suruYonetici.suSeviyesiY > -suSeviyesiUyariMesafesi)
                sonYon.y = -kacisGucu;

            // Çift çağırım silindi
            if (dipMekanigi != null)
                sonYon = dipMekanigi.YonVeHizHesapla(this, sonYon);

            Vector3 yatayHedef = new Vector3(hedefYon.x, 0, hedefYon.z);
            Vector3 yataySon = new Vector3(sonYon.x, 0, sonYon.z);
            float donusAcisi = Vector3.Angle(yatayHedef, yataySon.normalized);

            if (donusAcisi > keskinDonusAcisi && Time.time - sonTurnZamani > 2f && engelKarari == Vector3.zero)
            {
                bool ozelHareketYapiyorMu = (aktifHizCarpani != 1f);

                if (!ozelHareketYapiyorMu)
                {
                    turnBaslangicYonu = hedefYon;
                    turnHedefYonu = sonYon.normalized;
                    TurnYap();
                }
            }

            if (sonYon.magnitude > 0.01f)
            {
                float donus = suruYonetici != null ? suruYonetici.donusHizi : 2f;
                hedefYon = Vector3.Lerp(hedefYon, sonYon.normalized, donus * Time.fixedDeltaTime);
                hedefYon.Normalize();
            }
        }
        else
        {
            hedefYon = turnHedefYonu;
            aktifHizCarpani = turnHizYuzdesi;
        }

        Vector3 yeniPozisyon = transform.position + hedefYon * (hiz * aktifHizCarpani) * Time.fixedDeltaTime;

        if (suruYonetici != null)
        {
            yeniPozisyon.y = Mathf.Min(yeniPozisyon.y, suruYonetici.suSeviyesiY - 0.1f);
            yeniPozisyon.z = Mathf.Max(yeniPozisyon.z, suruYonetici.kiyiSiniriZ);
        }

        if (dipMekanigi != null)
        {
            yeniPozisyon = dipMekanigi.ZeminKorumasiUygula(yeniPozisyon, hedefYon, this);
            bakisYonu = dipMekanigi.BakisYonuHesapla(hedefYon);
        }
        else
        {
            RaycastHit zeminHit;
            // GetMask çöpe atıldı, Cache'den alınıyor
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out zeminHit, 5f, duvarLayerMaskesi))
            {
                float guvenliZemin = zeminHit.point.y + 0.15f;
                if (yeniPozisyon.y < guvenliZemin)
                {
                    yeniPozisyon.y = guvenliZemin;
                    if (hedefYon.y < 0)
                    {
                        hedefYon.y = 0f;
                        hedefYon.Normalize();
                    }
                }
            }

            bakisYonu = hedefYon;
            bakisYonu.y *= 0.2f;
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.MovePosition(yeniPozisyon);
            if (bakisYonu.magnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(bakisYonu, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(transform.rotation, hedefRot, turnRotateHizi * Time.fixedDeltaTime));
            }
        }
        else
        {
            transform.position = yeniPozisyon;
            if (bakisYonu.magnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(bakisYonu, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, turnRotateHizi * Time.fixedDeltaTime);
            }
        }
    }

    void TurnYap()
    {
        if (animator == null) return;

        turnOynuyor = true;
        sonTurnZamani = Time.time;

        Vector3 cross = Vector3.Cross(turnBaslangicYonu, turnHedefYonu);
        if (cross.y > 0)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        animator.CrossFade("Armature|Turn", 0.25f);
        StartCoroutine(TurnaDon());
    }

    IEnumerator TurnaDon()
    {
        yield return new WaitForSeconds(turnSuresi);

        if (animator != null)
            animator.CrossFade("Armature|Swim 1", 0.25f);

        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        turnOynuyor = false;
    }

    Vector3 EngelKontrolu()
    {
        Vector3 kacisYonu = Vector3.zero;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, engelGormeMesafesi, engelKatmani))
            kacisYonu += hit.normal;
        return kacisYonu.normalized;
    }

    Vector3 SinirKontrolu()
    {
        if (suruYonetici == null) return Vector3.zero;
        Vector3 merkez = suruYonetici.suruMerkezi;
        Vector3 pozisyon = transform.position;
        Vector3 sinirYonu = Vector3.zero;

        if (pozisyon.x > merkez.x + suruYonetici.alanGenislikX / 2f) sinirYonu.x = -1;
        else if (pozisyon.x < merkez.x - suruYonetici.alanGenislikX / 2f) sinirYonu.x = 1;

        float maxZ = merkez.z + suruYonetici.alanDerinlikZ / 2f;
        float minZ = suruYonetici.kiyiSiniriZ + 0.5f;
        if (pozisyon.z > maxZ) sinirYonu.z = -1;
        else if (pozisyon.z < minZ) sinirYonu.z = 1;

        float maxY = merkez.y + suruYonetici.alanYukseklikY / 2f;
        float minY = merkez.y - suruYonetici.alanYukseklikY / 2f;
        if (pozisyon.y > maxY) sinirYonu.y = -1;
        else if (pozisyon.y < minY) sinirYonu.y = 1;

        return sinirYonu.normalized;
    }

    Vector3 RastgeleYon()
    {
        if (Random.value < 0.02f)
        {
            Vector3 rastgele = Random.insideUnitSphere;
            rastgele.y = 0;
            return rastgele.normalized;
        }
        return Vector3.zero;
    }
}