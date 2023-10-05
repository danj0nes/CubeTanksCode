
/*
    This script is for the player aim. It creates an aim guide in the direction passed from the aiming joystick. 
    The aim guide will reflect off walls. 
*/

using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Linq;
using System.Collections.Generic;
public class Aim : MonoBehaviour
{
    private Shoot shoot;
    private Joystick joystick;
    [SerializeField] private Color cantShootColor = new(154f / 255f, 27f / 255f, 27f / 255f, 116f / 255f);
    private Color canShootColor;
    private Material aimGuide1Mat;
    private Material aimGuide2Mat;
    private Material aimGuide3Mat;
    [SerializeField] private GameObject aimPrefab;
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private float lineHeight = 0.1f;
    private LayerMask obstacleLayerMask = ~(1 << 7);
    private GameObject aimGuide1;
    private GameObject aimGuide2;
    private GameObject aimGuide3;
    private Vector3 aimDirection;
    private RaycastHit firstHitInfo;
    private RaycastHit secondHitInfo;

    private Vector3 shootDirection;
    private Vector3 prevShootDirection = Vector3.zero;

    private bool hasAimed;
    private bool begin;
    private bool thirdHit;

    [HideInInspector] public float bulletRadius = 0.5f;
    private float bulletHalfLength = 1f;
    [HideInInspector] public float totalBulletDistance;
    private float bulletDistance;

    private MapGenerator mapGenerator;

    public void StartAim(Joystick joystick, TankContainer tankValues, MapGenerator mapGenerator)
    {
        shoot = GetComponent<Shoot>();
        shoot.StartShoot(tankValues, transform, NoBullets, HasBullets);
        this.joystick = joystick;
        this.joystick.PointerUpResponse.Add(ReleasedAim);
        this.mapGenerator = mapGenerator;
        // Instantiate the aim object and hide it
        aimGuide1 = Instantiate(aimPrefab, Vector3.zero, Quaternion.identity);
        aimGuide1.SetActive(false);

        aimGuide2 = Instantiate(aimPrefab, Vector3.zero, Quaternion.identity);
        aimGuide2.SetActive(false);

        aimGuide1Mat = aimGuide1.GetComponent<Renderer>().material;
        aimGuide2Mat = aimGuide2.GetComponent<Renderer>().material;
        canShootColor = aimGuide1Mat.color;

        totalBulletDistance = tankValues.bulletRange;
        bulletRadius = tankValues.bulletType.Scale().x / 2f;
        bulletHalfLength = tankValues.bulletType.Scale().z / 2f;

        begin = true;
    }


    private void FixedUpdate()
    {
        if (!begin) { return; }

        // Get the aim direction from the joystick
        aimDirection = joystick.Direction;
        
        UpdateAimGuides();
    }

    private void UpdateAimGuides()
    {
        if (aimDirection.magnitude > 0f)
        {
            hasAimed = true;
            bulletDistance = totalBulletDistance;

            if (Physics.SphereCast(transform.position, bulletRadius, aimDirection, out firstHitInfo, bulletDistance, obstacleLayerMask))
            {
                CalculateAimGuideAfterHit(aimDirection, transform.position, firstHitInfo.point, aimGuide1);

                if (firstHitInfo.collider.CompareTag("Wall"))
                {
                    if (thirdHit)
                    {
                        CalculateNextTwoAimGuideWall(firstHitInfo, aimDirection, aimGuide2, true);
                    }
                    else
                    {
                        CalculateNextAimGuideWall(firstHitInfo, aimGuide2);
                    }
                }
                else
                {
                    aimGuide2.SetActive(false);
                    if (thirdHit) { aimGuide3.SetActive(false); }
                }
            }
            else
            {
                aimGuide2.SetActive(false);
                if (thirdHit) { aimGuide3.SetActive(false); }
                CalculateAimGuideNoObstacle(transform.position, aimDirection, aimGuide1);
            }
            shootDirection = aimDirection;
        }
        else 
        {
            if (hasAimed) { shootDirection = Vector3.zero; }
            HideGuides();
        }
    }


    private void CalculateNextAimGuideWall(RaycastHit hitInfo, GameObject aimGuide)
    {
        (Vector3 normal, Collider[] ignoreColliders) = hitInfo.collider.gameObject.GetComponent<Wall>().GetAlteredNormal(hitInfo.point, hitInfo.normal);

        var direction = Vector3.Reflect(aimDirection, normal);

        if (ignoreColliders.Length == 2)
        {
            RaycastHit[] sphereCastHits = Physics.SphereCastAll(hitInfo.point - aimDirection * bulletHalfLength, bulletRadius, direction, bulletDistance, obstacleLayerMask);
            sphereCastHits = sphereCastHits.Where(spherehit => !ignoreColliders.Contains(spherehit.collider)).OrderBy(x => x.distance).ToArray();

            if (sphereCastHits.Length > 0)
            {
                CalculateAimGuideAfterHit(direction, hitInfo.point, sphereCastHits[0].point, aimGuide);
                return;
            }
        }
        else if (Physics.SphereCast(hitInfo.point - aimDirection * bulletHalfLength, bulletRadius, direction, out secondHitInfo, bulletDistance, obstacleLayerMask))
        {
            CalculateAimGuideAfterHit(direction, hitInfo.point, secondHitInfo.point, aimGuide);
            return;
        }
        
        
        CalculateAimGuideNoObstacle(hitInfo.point, direction, aimGuide);
    }

    private void CalculateAimGuideAfterHit(Vector3 direction, Vector3 from, Vector3 to, GameObject aimGuide)
    {
        aimGuide.SetActive(true);
        float aimLength = Vector3.Distance(from, to);
        aimGuide.transform.position = Vector3.Lerp(from, to, 0.5f);
        aimGuide.transform.localScale = new Vector3(lineWidth, lineHeight, aimLength);
        aimGuide.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        bulletDistance -= aimLength;
    }
 
    private void CalculateAimGuideNoObstacle(Vector3 from, Vector3 direction, GameObject aimGuide)
    {
        aimGuide.SetActive(true);
        Vector3 to = from + direction.normalized * bulletDistance;
        aimGuide.transform.position = Vector3.Lerp(from, to, 0.5f);
        aimGuide.transform.localScale = new Vector3(lineWidth, lineHeight, bulletDistance);
        aimGuide.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }


    public void ReleasedAim() 
    {
        if (shootDirection != Vector3.zero)
        {
            shoot.Fire(shootDirection);
            
            prevShootDirection = shootDirection;
            shootDirection = Vector3.zero;
            hasAimed = false;
        }
        else if (!hasAimed)
        {
            float shortest_distance = totalBulletDistance + 10f;
            Vector3 shortest_direction = prevShootDirection;
            if (shortest_direction == Vector3.zero)
            {
                shortest_direction = transform.rotation * Vector3.forward;
            }
            else
            {
                Invoke(nameof(ResetDefaultAutoAim), 1.5f);   
            }
            List<Transform> enemyTankTransforms = new(mapGenerator.Enemies.Select(x => x.transform));
            foreach (Transform tankTransform in enemyTankTransforms)
            {
                float distance = Vector3.Distance(tankTransform.position, transform.position);
                if (distance < shortest_distance)
                {
                    Vector3 direction = tankTransform.position - transform.position;
                    if (Physics.SphereCast(transform.position, bulletRadius, direction, out RaycastHit hitInfo, shortest_distance, obstacleLayerMask))
                    {
                        if (hitInfo.collider.CompareTag(tankTransform.gameObject.tag))
                        {
                            shortest_distance = distance;
                            shortest_direction = direction;
                        }
                    }

                }

            }

            shoot.Fire(shortest_direction);
            
            prevShootDirection = shortest_direction;

        }
    }

    public void NoBullets() 
    {
        aimGuide1Mat.color = cantShootColor;
        aimGuide2Mat.color = cantShootColor;
        if (thirdHit) 
        {
            aimGuide3Mat.color = cantShootColor;
        }
    }
    public void HasBullets() 
    {
        aimGuide1Mat.color = canShootColor;
        aimGuide2Mat.color = canShootColor;

        if (thirdHit)
        {
            aimGuide3Mat.color = canShootColor;
        }
    }

    public void HideGuides() 
    {
        aimGuide1.SetActive(false);
        aimGuide2.SetActive(false);

        if (thirdHit)
        {
            aimGuide3.SetActive(false);
        }
    }

    private void ResetDefaultAutoAim() 
    {
        prevShootDirection = Vector3.zero;
    }

    public List<AimGuideLine> GetAimGuideLines() 
    {
        static AimGuideLine GetGuideLine(Transform tf) 
        {
            AimGuideLine line;
            Vector3 halfDirection = (tf.rotation * Vector3.forward).normalized * (tf.localScale.z / 2f);
            line.to = tf.position + halfDirection;
            line.from = tf.position - halfDirection;
            return line;
        }
        List<AimGuideLine> guideLines = new();
        if (aimGuide1.activeSelf) 
        {
            guideLines.Add(GetGuideLine(aimGuide1.transform));
        }
        if (aimGuide2.activeSelf) 
        {
            guideLines.Add(GetGuideLine(aimGuide2.transform));
        }

        return guideLines;
    }

    public void RangedGadget(bool activate) 
    {
        if (activate)
        {
            if (aimGuide3 == null)
            {
                aimGuide3 = Instantiate(aimPrefab, Vector3.zero, Quaternion.identity);
                aimGuide3.SetActive(false);
                aimGuide3Mat = aimGuide3.GetComponent<Renderer>().material;
            }

            if (aimGuide2Mat.color == cantShootColor) { aimGuide3Mat.color = cantShootColor; }

            thirdHit = true;
        }
        else 
        {
            thirdHit = false;
            aimGuide3.SetActive(false);
        }
    }

    private void CalculateNextTwoAimGuideWall(RaycastHit hitInfo, Vector3 prevDirection, GameObject aimGuide, bool first)
    {
        (Vector3 normal, Collider[] ignoreColliders) = hitInfo.collider.gameObject.GetComponent<Wall>().GetAlteredNormal(hitInfo.point, hitInfo.normal);

        var direction = Vector3.Reflect(prevDirection, normal);

        void AfterHit() 
        {
            CalculateAimGuideAfterHit(direction, hitInfo.point, secondHitInfo.point, aimGuide);

            if (first)
            {
                if (secondHitInfo.collider.CompareTag("Wall"))
                {
                    CalculateNextTwoAimGuideWall(secondHitInfo, direction, aimGuide3, false);
                }
                else
                {
                    aimGuide3.SetActive(false);
                }
            }
        }

        if (ignoreColliders.Length == 2)
        {
            RaycastHit[] sphereCastHits = Physics.SphereCastAll(hitInfo.point - prevDirection * bulletHalfLength, bulletRadius, direction, bulletDistance, obstacleLayerMask);
            sphereCastHits = sphereCastHits.Where(spherehit => !ignoreColliders.Contains(spherehit.collider)).OrderBy(x => x.distance).ToArray();

            secondHitInfo = sphereCastHits[0];

            if (sphereCastHits.Length > 0)
            {
                AfterHit();
                return;
            }
        }
        else if (Physics.SphereCast(hitInfo.point - prevDirection * bulletHalfLength, bulletRadius, direction, out secondHitInfo, bulletDistance, obstacleLayerMask))
        {
            AfterHit();
            return;
        }

        if (first)
        {
            aimGuide3.SetActive(false);
        }
        CalculateAimGuideNoObstacle(hitInfo.point, direction, aimGuide);
    }
}

public struct AimGuideLine
{
    public Vector3 to;
    public Vector3 from;
}