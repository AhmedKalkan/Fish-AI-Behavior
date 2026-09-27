using System.Collections;
using UnityEngine;

public class YemSaldirisi : MonoBehaviour
{
    [Header("Yem Algılama")]
    public float algilamaMenzili = 15f;
    public string yemTag = "Yem";

    [Header("Ortak Saldırı Ayarları")]
    public float saldiriHizi = 8f;
    public float dolanmaHizi = 4f;
    public float isirmaMenzili = 0.3f;
    public float dolanmaYaricapi = 1.5f;

    [Header("Geri Çekilme")]
    public float geriCekilmeMesafesi = 0.3f;
    public float geriCekilmeSuresi = 0.15f;
    public float rastgelelikMiktari = 0.5f;

    private BalikMotoru motor;
    private Animator animator;
    private Transform yemHedef;
    private bool yemGoruldu = false;
    private bool saldiriyor = false;
    private float sonSaldiriZamani = -999f;
    private Vector3 dolanmaNoktasi;
    private float yeniNoktaZamani = 0f;
    private Coroutine aktifKorotin;

    private float dogumZamani;

    void Awake()
    {
        MonoBehaviour aksiyonKodu = GetComponent("BalikAksiyon") as MonoBehaviour;
        if (aksiyonKodu != null) aksiyonKodu.enabled = false;
    }

    void Start()
    {
        motor = GetComponent<BalikMotoru>();
        animator = GetComponent<Animator>();
        dogumZamani = Time.time;
    }

    void Update()
    {
        if (motor == null || motor.suruYonetici == null) return;

        if (Time.time < dogumZamani + 2f) return;

        YemiBul();

        if (yemGoruldu && yemHedef != null && !yemHedef.CompareTag("YemAlindi"))
        {
            motor.yemModu = true;

            // 🎯 TEST MODU: Bekleme süresi 0! Yemi gördüğü gibi saldırır.
            if (!saldiriyor && Time.time - sonSaldiriZamani > 0f)
            {
                if (aktifKorotin != null) StopCoroutine(aktifKorotin);
                aktifKorotin = StartCoroutine(YemeSaldir());
            }

            if (!saldiriyor) YemEtrafindaDolan();
        }
        else
        {
            if (aktifKorotin != null)
            {
                StopCoroutine(aktifKorotin);
                aktifKorotin = null;
            }
            saldiriyor = false;
            motor.yemModu = false;
        }
    }

    void YemiBul()
    {
        Collider[] colliderlar = Physics.OverlapSphere(transform.position, algilamaMenzili);
        yemGoruldu = false;
        yemHedef = null;

        foreach (Collider col in colliderlar)
        {
            if (col.CompareTag(yemTag) && !col.CompareTag("YemAlindi"))
            {
                Vector3 yemPoz = col.transform.position;
                if (yemPoz.y > motor.suruYonetici.suSeviyesiY + 0.2f) continue;

                yemHedef = col.transform;
                yemGoruldu = true;
                break;
            }
        }
    }

    Vector3 ZeminKontrol(Vector3 hedefPozisyon)
    {
        DipMekanigi dip = GetComponent<DipMekanigi>();
        if (dip == null) return hedefPozisyon;

        int duvarLayer = LayerMask.GetMask("Duvar");
        RaycastHit hit;
        if (Physics.Raycast(hedefPozisyon + Vector3.up * 1f, Vector3.down, out hit, 4f, duvarLayer))
        {
            float guvenliZeminY = hit.point.y + 0.05f;
            if (hedefPozisyon.y < guvenliZeminY) hedefPozisyon.y = guvenliZeminY;
        }
        return hedefPozisyon;
    }

    void YemEtrafindaDolan()
    {
        if (yemHedef == null) return;

        if (Time.time > yeniNoktaZamani)
        {
            float rastgeleAci = Random.Range(0f, 360f);
            float rastgeleYaricap = dolanmaYaricapi + Random.Range(-rastgelelikMiktari, rastgelelikMiktari);
            Vector3 rastgeleYon = new Vector3(Mathf.Cos(rastgeleAci * Mathf.Deg2Rad), 0, Mathf.Sin(rastgeleAci * Mathf.Deg2Rad));
            dolanmaNoktasi = ZeminKontrol(yemHedef.position + rastgeleYon * rastgeleYaricap);
            yeniNoktaZamani = Time.time + Random.Range(1f, 3f);
        }

        transform.position = Vector3.MoveTowards(transform.position, dolanmaNoktasi, dolanmaHizi * Time.deltaTime);
        transform.position = ZeminKontrol(transform.position);

        if (yemHedef != null)
        {
            Vector3 yemeBakis = (yemHedef.position - transform.position).normalized;
            if (yemeBakis.magnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(yemeBakis, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, 5f * Time.deltaTime);
            }
        }
    }

    Transform Bul(Transform parent, string isim)
    {
        if (parent.name == isim) return parent;
        foreach (Transform child in parent)
        {
            Transform bulundu = Bul(child, isim);
            if (bulundu != null) return bulundu;
        }
        return null;
    }

    IEnumerator YemeSaldir()
    {
        if (yemHedef == null || !yemGoruldu || yemHedef.CompareTag("YemAlindi"))
        {
            saldiriyor = false;
            yield break;
        }

        saldiriyor = true;

        while (yemHedef != null && Vector3.Distance(transform.position, yemHedef.position) > isirmaMenzili)
        {
            if (!yemGoruldu || yemHedef == null || yemHedef.CompareTag("YemAlindi"))
            {
                saldiriyor = false;
                yield break;
            }

            Vector3 yon = (yemHedef.position - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, yemHedef.position, saldiriHizi * Time.deltaTime);
            transform.position = ZeminKontrol(transform.position);

            if (yon.magnitude > 0.01f)
            {
                Vector3 bakisYonu = yon;
                if (GetComponent<DipMekanigi>() != null)
                {
                    int duvarLayer = LayerMask.GetMask("Duvar");
                    RaycastHit zeminHit;
                    if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out zeminHit, 2f, duvarLayer))
                        bakisYonu = Vector3.ProjectOnPlane(yon, zeminHit.normal);
                }

                if (bakisYonu.magnitude > 0.01f)
                {
                    Quaternion hedefRot = Quaternion.LookRotation(bakisYonu, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, 15f * Time.deltaTime);
                }
            }

            yield return null;
        }

        if (yemHedef != null && animator != null && !yemHedef.CompareTag("YemAlindi"))
        {
            yemHedef.tag = "YemAlindi";

            // 🎯 TEST MODU: Tutma Şansı KESİN %100! (Tırtıklayıp kaçma ihtimali silindi)
            Debug.Log(gameObject.name + " YEMİ KAPTI! 🎣");

            SualtiKamerasi kameraSistemi = Object.FindFirstObjectByType<SualtiKamerasi>();
            if (kameraSistemi != null) kameraSistemi.BalikYakalandi(this.transform);

            FishingLine misina = Object.FindFirstObjectByType<FishingLine>();
            if (misina != null)
            {
                misina.kancadaBalikVar = true;
                misina.yemiGoster = false;
            }

            float balikBoyu = Mathf.Abs(transform.localScale.x);
            OltaSway oltaSway = Object.FindFirstObjectByType<OltaSway>();
            if (oltaSway != null) oltaSway.anlikAgirlik = balikBoyu * 15f;

            Transform agizNoktasi = Bul(transform, "AgizNoktasi");
            Vector3 fark = agizNoktasi != null ? transform.position - agizNoktasi.position : Vector3.zero;
            Vector3 yemPoz = yemHedef.position;
            Transform yemParent = yemHedef.parent;

            Destroy(yemHedef.gameObject);

            transform.SetParent(yemParent);
            transform.position = ZeminKontrol(yemPoz + fark);

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (motor != null) motor.enabled = false;
            enabled = false;

            animator.CrossFade("Armature|Hooked", 0.1f);

            float hookedSayac = 0f;
            while (hookedSayac < 1.66f)
            {
                if (misina != null && misina.segmentler != null && misina.segmentler.Count > 0)
                {
                    Transform agiz = Bul(transform, "AgizNoktasi");
                    Vector3 baglantiPoz = agiz != null ? agiz.position : transform.position;
                    int sonIndex = misina.segmentler.Count - 1;
                    misina.segmentler[sonIndex].pozisyon = baglantiPoz;
                    misina.segmentler[sonIndex].oncekiPozisyon = baglantiPoz;
                }
                hookedSayac += Time.deltaTime;
                yield return null;
            }

            MonoBehaviour aksiyon = GetComponent("BalikAksiyon") as MonoBehaviour;
            if (aksiyon != null)
            {
                aksiyon.enabled = true;
                var alan = aksiyon.GetType().GetField("suSeviyesiY");
                if (alan != null) alan.SetValue(aksiyon, 9.78f);
            }

            saldiriyor = false;
            yield break;
        }

        // 🎯 Tırtıklayıp geri çekilme kodları silindi.

        saldiriyor = false;
        sonSaldiriZamani = Time.time;
    }
}