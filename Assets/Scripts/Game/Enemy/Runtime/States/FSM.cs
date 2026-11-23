using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemy
{
    public enum StateType
    {
        Patrol, AlertHigh, AlertLow, Reposition, Attack, Guard, Hit, Death
    }

    [Serializable]
    public class Parameter
    {
        public int health;
        public float moveSpeed;
        public float chaseSpeed;
        public float idleTime;
        public Transform[] patrolPoints;
        public Transform[] chasePoints;
        public Transform target;
        public Animator animator;
    }
    public class FSM : MonoBehaviour
    {
        public Parameter parameter;
        private IState currentState;

        private Dictionary<StateType, IState> states = new Dictionary<StateType, IState>();
        // Start is called before the first frame update
        void Start()
        {
            states.Add(StateType.Patrol,new PatrolState(this));
            
            TransitionState(StateType.Patrol);
            parameter.animator = GetComponent<Animator>();
        }

        // Update is called once per frame
        void Update()
        {
            currentState.OnUpdate();
        }

        public void TransitionState(StateType type)
        {
            if (currentState != null)
            {
                currentState.OnExit();
            }

            currentState = states[type];
            currentState.OnEnter();
        }

        public void FlipTo(Transform target)
        {
            if (target != null)
            {
                if (transform.position.x > target.position.x)
                {
                    transform.localScale = new Vector3(-1, 1, 1);
                }
                else if (transform.position.x < target.position.x)
                {
                    transform.localScale = new Vector3(1, 1, 1);
                }
            }
        }
    }
}
