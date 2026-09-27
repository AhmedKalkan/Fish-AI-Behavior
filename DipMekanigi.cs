using UnityEngine;
using System.Collections;

public class DipMekanigi : MonoBehaviour
{
    [Header("Doðal Beslenme (Feeding)")]
    public float beslenmeYurumeHizi = 0.5f;
    private bool dogalBesleniyor = false;
    private bool beslenmeAnimasyonuAktif = false;
    private float siradakiBeslenmeZamani = 0f;

    [Header("Özel Hareket (Slide)")]
    public float slideYurumeHiziCarpani = 1.8f;
    public float slideAtilmaHiziCarpani = 1.5f;
    public float slideAtilmaSuresi = 0.4f;

    private bool slideYapiyor = false;
    private bool slideAnimasyonuAktif = false;
    private float siradakiSlideZamani = 0f;

    private Animator animator;
    private BalikMotoru motor;

    void Start()
    {
        animator = GetComponent<Animator>();
        motor = GetComponent<BalikMotoru>();
        siradakiBeslenmeZamani = Time.time + Random.Range(5f, 15f);
        siradakiSlideZamani = Time.time + Random.Range(15f, 35f);
    }

    void Update()
    {
        if (motor == null || motor.yemModu)
        {
            dogalBesleniyor = false;
            slideYapiyor = false;
            return;
        }

        if (!dogalBesleniyor && !slideYapiyor && Time.time > siradakiBeslenmeZamani)
            StartCoroutine(DogalZeminBeslenmeRutin());

        if (!dogalBesleniyor && !slideYapiyor && Time.time > siradakiSlideZamani)
            StartCoroutine(SlideRutin());
    }

    public Vector3 YonVeHizHesapla(BalikMotoru anaMotor, Vector3 mevcutYon)
    {
        if (dogalBesleniyor || slideYapiyor)
        {
            mevcutYon.y = -1.2f;
            mevcutYon.x *= 0.3f;
            mevcutYon.z *= 0.3f;
        }
        else mevcutYon.y *= 0.5f;

        if (beslenmeAnimasyonuAktif) anaMotor.aktifHizCarpani = beslenmeYurumeHizi;
        else if (slideAnimasyonuAktif) anaMotor.aktifHizCarpani = slideYurumeHiziCarpani;

        return mevcutYon;
    }

    public Vector3 ZeminKorumasiUygula(Vector3 yeniPozisyon, Vector3 hedefYon, BalikMotoru anaMotor)
    {
        int duvarLayer = LayerMask.GetMask("Duvar");
        RaycastHit zeminHit;
        if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out zeminHit, 5f, duvarLayer))
        {
            float guvenliZemin = zeminHit.point.y + 0.05f;
            if (yeniPozisyon.y < guvenliZemin)
            {
                yeniPozisyon.y = guvenliZemin;
                if (hedefYon.y < 0)
                {
                    anaMotor.hedefYon.y = 0f;
                    anaMotor.hedefYon.Normalize();
                }
            }
        }
        return yeniPozisyon;
    }

    public Vector3 BakisYonuHesapla(Vector3 hedefYon)
    {
        Vector3 bakisYonu = hedefYon;
        int duvarLayer = LayerMask.GetMask("Duvar");
        RaycastHit egimHit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out egimHit, 2f, duvarLayer))
        {
            float zemineGercek = egimHit.distance - 0.5f;
            if (zemineGercek < 0.15f) bakisYonu = Vector3.ProjectOnPlane(hedefYon, egimHit.normal).normalized;
            else bakisYonu.y *= 0.8f;
        }
        else bakisYonu.y *= 0.8f;

        return bakisYonu;
    }

    IEnumerator DogalZeminBeslenmeRutin()
    {
        dogalBesleniyor = true;
        beslenmeAnimasyonuAktif = false;
        int duvarLayer = LayerMask.GetMask("Duvar");
        bool dibeUlasti = false;
        float dalisZamanAsimi = Time.time + 10f;

        while (!dibeUlasti && Time.time < dalisZamanAsimi)
        {
            if (motor.yemModu || slideYapiyor) { dogalBesleniyor = false; yield break; }
            RaycastHit zeminHit;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out zeminHit, 5f, duvarLayer))
                if ((zeminHit.distance - 1f) <= 0.15f) dibeUlasti = true;
            yield return null;
        }

        beslenmeAnimasyonuAktif = true;
        int tekrarSayisi = Random.Range(1, 4);

        for (int i = 0; i < tekrarSayisi; i++)
        {
            if (motor.yemModu || slideYapiyor) { dogalBesleniyor = false; beslenmeAnimasyonuAktif = false; yield break; }
            string yemeAnimasyonu = Random.value > 0.5f ? "Armature|Feeding 1" : "Armature|Feeding 2";
            if (animator != null) animator.CrossFade(yemeAnimasyonu, 0.3f);
            yield return new WaitForSeconds(Random.Range(2f, 3.5f));
        }

        beslenmeAnimasyonuAktif = false;
        if (animator != null && !motor.yemModu) animator.CrossFade("Armature|Swim 1", 0.5f);
        dogalBesleniyor = false;
        siradakiBeslenmeZamani = Time.time + Random.Range(10f, 30f);
    }

    IEnumerator SlideRutin()
    {
        slideYapiyor = true;
        slideAnimasyonuAktif = false;
        int duvarLayer = LayerMask.GetMask("Duvar");
        bool dibeUlasti = false;
        float dalisZamanAsimi = Time.time + 10f;

        while (!dibeUlasti && Time.time < dalisZamanAsimi)
        {
            if (motor.yemModu || dogalBesleniyor) { slideYapiyor = false; yield break; }
            RaycastHit zeminHit;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out zeminHit, 5f, duvarLayer))
                if ((zeminHit.distance - 1f) <= 0.2f) dibeUlasti = true;
            yield return null;
        }

        slideAnimasyonuAktif = true;
        if (Random.value > 0.5f) transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        if (animator != null) animator.CrossFade("Armature|Slide", 0.2f);
        yield return new WaitForSeconds(0.3f);

        float atilmaSayac = 0f;
        Vector3 atilmaYonu = transform.forward;
        while (atilmaSayac < slideAtilmaSuresi)
        {
            if (motor.yemModu) break;
            transform.position += atilmaYonu * (motor.hiz * slideAtilmaHiziCarpani) * Time.deltaTime;
            atilmaSayac += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        slideAnimasyonuAktif = false;
        if (animator != null && !motor.yemModu) animator.CrossFade("Armature|Swim 1", 0.3f);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        slideYapiyor = false;
        siradakiSlideZamani = Time.time + Random.Range(25f, 50f);
    }
}