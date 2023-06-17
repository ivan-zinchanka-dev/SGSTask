﻿using System;
using System.Collections.Generic;
using Adapters;
using Controllers;
using Environment;
using Managers;
using UnityEngine;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _speed = 0.1f;
        [SerializeField] private float _shootingCooldown = 1.0f;
        
        [Space]
        [SerializeField] private Joystick _motionJoystick;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _shotEffectSpawnPoint;
        [SerializeField] private CharacterEventsAdapter _eventsAdapter;
        [SerializeField] private FootstepsTrail _footstepsTrail;
        
        private Vector3 _motion = default;
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int JumpParam = Animator.StringToHash("Jump");

        private int _upperAvatarLayerIndex;

        private LinkedList<PlayerTarget> _currentTargets = new LinkedList<PlayerTarget>();

        private PlayerState _state = PlayerState.Walking;

        private float _timeBetweenShots = 0.0f;

        private void Awake()
        {
            _upperAvatarLayerIndex = _animator.GetLayerIndex("UpperAvatarLayer");
        }


        private void OnEnable()
        {
            _eventsAdapter.LeftFootStep.AddListener(LeaveLeftFootstep);
            _eventsAdapter.RightFootStep.AddListener(LeaveRightFootstep);
        }

        private void LeaveLeftFootstep()
        {
            _footstepsTrail.LeaveFootstep(transform.position, transform.rotation, false);
        }
        
        private void LeaveRightFootstep()
        {
            _footstepsTrail.LeaveFootstep(transform.position, transform.rotation, true);
        }

        private void Update()
        {
            switch (_state)
            {
                case PlayerState.Walking:
                    Walk();
                    break;
                
                case PlayerState.Shooting:
                    Shoot(); 
                    break;

                case PlayerState.Jump:
                    break;
                
                default:
                    break;
            }
            
            
        }

        private void Walk()
        {
            MoveByJoystick();
            
            if (_motion != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(_motion.normalized, Vector3.up);
            }
        }

        private void MoveByJoystick()
        {
            _motion.Set(_motionJoystick.Horizontal * _speed * Time.deltaTime, 0.0f, _motionJoystick.Vertical * _speed * Time.deltaTime);
            _characterController.Move(_motion);
            
            _animator.SetFloat(SpeedParam, Mathf.Max(Mathf.Abs(_motion.normalized.x), Mathf.Abs(_motion.normalized.z)));
        }

        private void Shoot()
        {
            MoveByJoystick();
            
            transform.rotation = Quaternion.LookRotation(GetDirectionTo(_currentTargets.First.Value.transform), Vector3.up);
            
            if (_timeBetweenShots > _shootingCooldown)
            {
                _currentTargets.First.Value.Fire(35);
                Debug.Log("Fire!");
                _timeBetweenShots = 0.0f;

                EffectsManager.Instance.MakeShot(_shotEffectSpawnPoint.transform.position);
            }
            else
            {
                _timeBetweenShots += Time.deltaTime;
            }
        }

        private Vector3 GetDirectionTo(Transform target)
        {
            return target.transform.position - transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PlayerTarget>(out PlayerTarget target))
            {
                AddShootingTarget(target);
            }
            else if (other.TryGetComponent<Gap>(out Gap gap))
            {
                JumpOver(gap);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<PlayerTarget>(out PlayerTarget target))
            {
                if (_currentTargets.Contains(target))
                {
                    RemoveShootingTarget(target);
                }
            }
        }

        private void AddShootingTarget(PlayerTarget target)
        {
            if (_currentTargets.Count == 0)
            {
                StartShooting();
            }

            _currentTargets.AddLast(target);
            target.OnDestroyed.AddListener(NextShootingTarget);
        }
        
        private void RemoveShootingTarget(PlayerTarget target)
        {
            if (_currentTargets.First.Value == target)
            {
                NextShootingTarget();
            }
            else
            {
                _currentTargets.Remove(target);
                target.OnDestroyed.RemoveListener(NextShootingTarget);
            }
        }
        
        private void StartShooting()
        {
            _state = PlayerState.Shooting;
            SetAimingAnimation(true);
        }

        private void NextShootingTarget()
        {
            PlayerTarget target = _currentTargets.First.Value;
            _currentTargets.RemoveFirst();
            target.OnDestroyed.RemoveListener(NextShootingTarget);

            if (_currentTargets.Count == 0)
            {
                StopShooting();
            }
        }

        private void StopShooting()
        {
            _state = PlayerState.Walking;
            SetAimingAnimation(false);
        }

        private void SetAimingAnimation(bool isActive)
        {
            _animator.SetLayerWeight(_upperAvatarLayerIndex, Convert.ToSingle(isActive));
        }

        private void JumpOver(Gap gap)
        {
            _state = PlayerState.Jump;
            _animator.SetTrigger(JumpParam);
            gap.JumpOver(transform, () =>
            {
                _state = PlayerState.Walking;
            });
        }
        
        private void OnDisable()
        {
            _eventsAdapter.LeftFootStep.RemoveListener(LeaveLeftFootstep);
            _eventsAdapter.RightFootStep.RemoveListener(LeaveRightFootstep);
        }
    }
}
