using System.Collections;
using UnityEngine;

namespace RailTrip3D
{
    // The die is a pre-authored Scene hierarchy, NOT a runtime-generated model.
    // The game result always comes from GameRules/online server, not the animation.
    public sealed class DiceRollView : MonoBehaviour
    {
        private Transform die;
        private Coroutine active;
        public bool IsRolling { get; private set; }
        public int LastFace { get; private set; }
        private static Quaternion FaceUp(int value)
        {
            switch(value)
            {
                case 1: return Quaternion.identity;
                case 2: return Quaternion.Euler(-90,0,0);
                case 3: return Quaternion.Euler(0,0,90);
                case 4: return Quaternion.Euler(0,0,-90);
                case 5: return Quaternion.Euler(90,0,0);
                default: return Quaternion.Euler(0,0,180);
            }
        }
        private void Bind()
        {
            if(die!=null)return;
            var board=FindFirstObjectByType<SceneRailBoard>();
            if(board!=null)die=board.diceVisual;
            if(die==null)Debug.LogError("Editable Scene is missing its authored dice object.");
        }
        public void Cancel()
        {
            if(active!=null)StopCoroutine(active);
            active=null;
            IsRolling=false;
            Bind();
            if(die!=null)die.gameObject.SetActive(false);
        }
        public void Play(int face, Vector3 boardPosition)
        {
            Cancel(); Bind();
            if(die==null)return;
            LastFace=Mathf.Clamp(face,1,6);
            active=StartCoroutine(Animate(LastFace,boardPosition));
        }
        private IEnumerator Animate(int face,Vector3 boardPosition)
        {
            IsRolling=true;
            die.gameObject.SetActive(true);
            Vector3 start=boardPosition+Vector3.up*3.2f;
            die.position=start;
            die.localScale=Vector3.one*1.22f;
            float t=0;
            Quaternion rotation=die.rotation;
            while(t<.95f)
            {
                t+=Time.deltaTime;
                float fraction=Mathf.Clamp01(t/.95f);
                float bounce=Mathf.Abs(Mathf.Sin(fraction*Mathf.PI*2.6f))*1.75f*(1f-.45f*fraction);
                die.position=start+Vector3.up*bounce;
                die.rotation=Quaternion.Euler(950*t,740*t,810*t);
                rotation=die.rotation;
                yield return null;
            }
            Quaternion settled=FaceUp(face);
            t=0;
            while(t<.4f)
            {
                t+=Time.deltaTime;
                float progress=Mathf.Clamp01(t/.4f);
                float easing=1f-Mathf.Pow(1f-progress,3f);
                die.rotation=Quaternion.Slerp(rotation,settled,easing);
                die.position=start+Vector3.up*(Mathf.Sin(progress*Mathf.PI)*.17f);
                yield return null;
            }
            die.rotation=settled;
            die.position=start;
            yield return new WaitForSeconds(.3f);
            die.gameObject.SetActive(false);
            IsRolling=false;
            active=null;
        }
    }
}
