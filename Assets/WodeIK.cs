using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WodeIK), true)]
public class WodeIKEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var ik = (WodeIK)target;
        if (GUILayout.Button("初始化"))
        {
            ik.OnEnable();
        }
        if (GUILayout.Button("跟随"))
        {
            ik.Follow();
        }
        if (GUILayout.Button("缓慢跟随"))
        {
            ik.Lerp();
        }
        if (GUILayout.Button("计算关节链（*迭代次数）"))
        {
            ik.SolveAll();
        }
        if (GUILayout.Button("计算下一个关节"))
        {
            ik.SolveStep();
        }
    }
}
[ExecuteInEditMode]
public class WodeIK : MonoBehaviour
{
    IKBoneChain chain;
    public List<Transform> joints;
    public Transform end;
    public Transform target;
    private Transform _n;
    private Transform n
    {
        get
        {
            if (_n == null)
            {

                _n = (new GameObject("Lerp")).transform;
                _n.position = target.position;
                _n.rotation = target.rotation;
            }
            return _n;
        }
    }
    private int index;
    private WRay ray = null;
    private bool follow;
    private bool lerp;

    [Header("参数预设")]
    public SettingsPrefabType settingPrefabType = SettingsPrefabType.Hair;
    private SettingsPrefabType _settingPrefabType = SettingsPrefabType.Hair;
    private Setting[] settingPrefab;
    public enum SettingsPrefabType
    {
        Hair,
        CurlyHair,
        Hand,
        Tentacle,
        Custom
    }
    public Setting setting;
    private float _boneLength;
    private int _jointCount;
    [System.Serializable]
    public struct Setting
    {
        [Header("骨骼长度")]
        [Range(0, 50)]
        public float boneLength;
        [Header("关节数")]
        [Range(1, 100)]
        public int jointCount;
        [Header("迭代次数")]
        public int iteration;
        [Header("每次迭代的最大旋转角度（相对值）")]
        public float maxDeltaAngle;
        [Header("关节最大角度（绝对值）")]
        public float maxAngle;
        [Header("关节最小角度（绝对值）")]
        public float minAngle;
        [Header("除了第一个关节以外只旋转X轴")]
        public bool xOnly;
    }
    public void Lerp()
    {
        ray = null;
        if (!lerp)
        {
            n.position = end.position;
            chain.target = n;
            follow = true;
            lerp = true;
        }
        else
        {
            var timeMultiplier = 3.6f;
            var minSpeed = 0.12f;
            if ((target.position - n.position).sqrMagnitude < Mathf.Pow(minSpeed, 2))
            {
                n.position = target.position;
                return;
            }
            else
            {
                var lerp = Vector3.Lerp(n.position, target.position, Time.deltaTime * timeMultiplier);
                var x = lerp - n.position;
                if (x.sqrMagnitude < Mathf.Pow(minSpeed, 2))
                {
                    n.position += x.normalized * minSpeed;
                }
                else n.position = lerp;
            }

        }
    }
    public void Follow()
    {
        chain.target = target;
        follow = true;
    }
    public void UpdateValues()
    {
        UpdateJoints();
        if (_boneLength != setting.boneLength)
        {
            _boneLength = setting.boneLength;
            ScaleChain();
        }
        int i = 0;
        foreach (var joint in chain.joints)
        {
            joint.maxAngle = setting.maxAngle;
            joint.minAngle = setting.minAngle;
            if (setting.xOnly)
            {
                joint.constraints = (i == chain.joints.Length - 1 ? RigidbodyConstraints.None :
                    RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ);
            }
            else { joint.constraints = RigidbodyConstraints.None; }
            i++;
        }
        chain.maxDeltaAngle = setting.maxDeltaAngle;
        chain.iteration = setting.iteration;
    }
    GameObject origin { get { if (_origin == null) _origin = GameObject.Find("Joint"); return _origin; } }
    GameObject _origin;
    void ChangeChain()
    {
        if (origin == null) return;
        if (_jointCount == setting.jointCount) return;
        _jointCount = setting.jointCount;
        UpdateChain();
        if (joints.Count < _jointCount)
        {
            for (int i = joints.Count; i < _jointCount; i++)
            {
                var n = Instantiate(origin, joints[i - 1]).transform;
                n.name = "Joint " + (i + 1).ToString();
                joints.Add(n);
            }
            end.SetParent(joints[joints.Count - 1]);
        }
        else if (joints.Count > _jointCount)
        {
            end.SetParent(joints[_jointCount - 1]);
            DestroyImmediate(joints[_jointCount].gameObject);

        }
        else { return; }
        UpdateChain();
        ScaleChain();
    }
    void ScaleChain()
    {
        for (int i = 0; i < joints.Count; i++)
        {
            var bone = joints[i].FindChild("Bone");
            bone.localPosition = new Vector3(0, 0, setting.boneLength * 0.5f + 0.5f);
            bone.localScale = new Vector3(1, 1, setting.boneLength);
            bone.GetComponent<MeshRenderer>().enabled = !IKTool.CloseZero(bone.localScale.z);
            joints[i].localPosition = new Vector3(0, 0, i == 0 ? 0 : 1 + setting.boneLength);
            joints[i].localRotation = Quaternion.identity;
        }
        end.localPosition = new Vector3(0, 0, setting.boneLength + 0.5f + end.localScale.z * 0.5f);
        end.SetParent(joints[joints.Count - 1]);
    }
    void UpdateChain()
    {
        var first = joints[0];
        joints.Clear();
        foreach (var joint in first.GetComponentsInChildren<SphereCollider>()) if (joint.transform != end) joints.Add(joint.transform);
    }
    public void Update()
    {
        if (_settingPrefabType != settingPrefabType)
        {
            _settingPrefabType = settingPrefabType;
            if (settingPrefabType != SettingsPrefabType.Custom)
            {
                setting = settingPrefab[(int)settingPrefabType];
            }
        }
        else if (settingPrefabType != SettingsPrefabType.Custom && !setting.Equals(settingPrefab[(int)settingPrefabType]))
        {
            settingPrefabType = SettingsPrefabType.Custom;
            _settingPrefabType = settingPrefabType;
        }
        ChangeChain();
        UpdateValues();
        if (lerp) Lerp();
        if (follow) SolveAll();
        if (ray != null) ray.Draw();
    }
    void UpdateJoints()
    {
        var l = new List<Joint>();
        for (int i = joints.Count - 1; i >= 0; i--)
        {
            l.Add(new Joint(joints[i]));
        }
        chain.joints = l.ToArray();
    }
    public void DoReset()
    {
        chain = new IKBoneChain();
        if (joints.Count == 0) return;
        ChangeChain();
        joints[0].transform.localPosition = Vector3.zero;
        joints[0].parent.transform.localPosition = Vector3.zero;

        chain.end = end;
        chain.target = target;
        ScaleChain();
        UpdateValues();
        lerp = false;
        follow = false;
        index = 0;
        ray = null;
    }
    public void OnEnable()
    {
        settingPrefab = new Setting[] {
        new Setting() { boneLength = 5,jointCount =50, iteration = 100, maxDeltaAngle = 0.1f,maxAngle =360 ,minAngle =-360, xOnly = true },
        new Setting() { boneLength = 5,jointCount =50,iteration = 100, maxDeltaAngle = 360,maxAngle =30 ,minAngle =-30, xOnly = false },
        new Setting() { boneLength = 50,jointCount =2,iteration = 20, maxDeltaAngle = 360,maxAngle =360 ,minAngle =-360, xOnly = false },
        new Setting() { boneLength = 1.2f,jointCount =40,iteration = 20, maxDeltaAngle = 25,maxAngle =360 ,minAngle =-360, xOnly = false }};
        if (target == null) return;
        DoReset();
        foreach (var t in FindObjectsOfType<Transform>()) if (t.name == "Lerp") DestroyImmediate(t.gameObject);
    }

    public void SolveStep()
    {
        UpdateValues();
        ray = chain.Solve(index % chain.joints.Length);
        index++;
    }
    public void SolveAll()
    {
        ray = null;
        UpdateValues();
        chain.Solve();
    }
}

