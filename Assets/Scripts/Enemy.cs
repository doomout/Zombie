using System.Collections;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 적 AI를 구현한다
public class Enemy : LivingEntity 
{
    public LayerMask whatIsTarget; // 추적 대상 레이어

    private LivingEntity targetEntity; // 추적할 대상
    private NavMeshAgent pathFinder; // 경로계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격시 재생할 파티클 효과
    public AudioClip deathSound; // 사망시 재생할 소리
    public AudioClip hitSound; // 피격시 재생할 소리

    private Animator enemyAnimator; // 애니메이터 컴포넌트
    private AudioSource enemyAudioPlayer; // 오디오 소스 컴포넌트
    private Renderer enemyRenderer; // 렌더러 컴포넌트

    public float damage = 10f; // 공격력
    public float timeBetAttack = 1.0f; // 공격 간격
    private float lastAttackTime; // 마지막 공격 시점

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget
    {
        get
        {
            // 추적할 대상이 존재하고, 대상이 사망하지 않았다면 true
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }

            // 그렇지 않다면 false
            return false;
        }
    }

    private void Awake() 
    {
        // 게임 오브젝트에서 사용할 컴포넌트 가져오기
        pathFinder = GetComponent<NavMeshAgent>();
        enemyAnimator = GetComponent<Animator>();
        enemyAudioPlayer = GetComponent<AudioSource>();

        //렌더러 컴포넌트는 자식 게임 오브젝트에 있음
        enemyRenderer = GetComponentInChildren<Renderer>();
    }

    // 적 AI의 능력치 
    public void Setup(float newHealth, float newDamage, float newSpeed, Color skinColor) 
    {
        //체력 설정
        startingHealth = newHealth;
        health = newHealth;
        
        //공격력 설정
        damage = newDamage;
        //이동 속도
        pathFinder.speed = newSpeed;
        //외형 색
        enemyRenderer.material.color = skinColor;
    }

    private void Start() 
    {
        // 게임 오브젝트 활성화와 동시에 AI의 추적 루틴 시작
        StartCoroutine(UpdatePath());
    }

    private void Update() 
    {
        // 추적 대상의 존재 여부에 따라 다른 애니메이션을 재생
        enemyAnimator.SetBool("HasTarget", hasTarget);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로를 갱신
    private IEnumerator UpdatePath() 
    {
        // 살아있는 동안 무한 루프
        while (!dead)
        {
            if(hasTarget)//추적 대상 존재
            {
                //ai 이동 모드
                pathFinder.isStopped = false;
                //경로를 갱신
                pathFinder.SetDestination(targetEntity.transform.position);
            }
            else //추적 대상이 없으면~
            {
                //ai 이동 중지
                pathFinder.isStopped = true;
                //20 유닛 반지름을 가진 가상의 구와 겹치는 whatIsTarget 레이어를 가진 콜라이더를 가져옴
                Collider[] colliders = Physics.OverlapSphere(transform.position, 20f, whatIsTarget);

                for (int i = 0; i < colliders.Length; i++)
                {
                    LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();

                    //livingEntity 컴포넌트가 존재하고, 죽지 않았으면
                    if (livingEntity != null && !livingEntity.dead)
                    {
                        //추적 대상을 해당 livingEntity로 설정
                        targetEntity = livingEntity;
                        break;
                    }
                }
            }
            // 0.25초 주기로 처리 반복
            yield return new WaitForSeconds(0.25f);
        }
    }

    // 데미지를 입었을때 실행할 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) 
    {
        if(!dead)
        {
            hitEffect.transform.position = hitPoint; //공격 받은 위치
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal); //피격 방향
            hitEffect.Play();

            enemyAudioPlayer.PlayOneShot(hitSound);
        }
        // LivingEntity의 OnDamage()를 실행하여 데미지 적용
        base.OnDamage(damage, hitPoint, hitNormal);
    }

    // 사망 처리
    public override void Die() 
    {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        base.Die();

        //게임 오브젝트에 추가된 모든 콜라이더 컴포넌트를 찾아서 비활성화 함
        Collider[] enemyColliders = GetComponents<Collider>();
        for (int i = 0; i < enemyColliders.Length; i++)
        {
            enemyColliders[i].enabled = false;
        }

        //내비메시 에이전트의 이동을 중단
        pathFinder.isStopped = true;
        pathFinder.enabled = false;

        //사망 효과음 재생
        enemyAnimator.SetTrigger("Die");
        enemyAudioPlayer.PlayOneShot(deathSound);
    }

    // 트리거 충돌한 상대방 게임 오브젝트가 추적 대상이라면 공격 실행
    private void OnTriggerStay(Collider other) 
    {
        //사망하지 않고, 최근 공격시점에서  timeBetAttack 이상의 시간이 지났다면 공격 가능
        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            //상대방의 LivingEntity 타입 가져오기
            LivingEntity attackTarget = other.GetComponent<LivingEntity>();

            //상대방의 LivingEntity 가 자신의 추적 대상이라면 공격 실행
            if (attackTarget != null && attackTarget == targetEntity)
            {
                //최근 공격 시간 갱신
                lastAttackTime = Time.time;

                //상대방의 피격 위치와 피격 방향을 근사값으로 계산
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position  - other.transform.position;

                //공격 실행
                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }
    }
}