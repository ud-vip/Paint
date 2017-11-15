﻿using Leap.Unity.Attributes;
using Leap.Unity.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Leap.Unity.PhysicalInterfaces {

  public class HandledObject : MovementObservingBehaviour, IHandle {

    #region Inspector

    [Header("Handles (each must be IHandle)")]
    //[ElementsImplementInterface(typeof(IHandle))]
    // TODO: Write a custom property drawer that renders ImplementsInterface fields
    // instead of plain Transform fields.
    [SerializeField, EditTimeOnly]
    private Transform[] _handles;
    public IIndexable<IHandle> handles {
      get {
        return new TransformArrayComponentWrapper<IHandle>(_handles);
      }
    }

    #endregion

    #region Unity Events

    private Dictionary<IHandle, Pose> _objToHandleDeltaPoses
      = new Dictionary<IHandle, Pose>();

    private IHandle _heldHandle = null;
    public IHandle heldHandle { get { return _heldHandle; } }

    protected virtual void Awake() {
      _targetPose = this.pose;
    }

    protected override void Update() {
      base.Update();

      var objPose = this.pose;

      if (_heldHandle != null && _heldHandle.wasReleased) {
        _heldHandle = null;
      }

      // Enforces only one handle is held at a time.
      // This isn't great, but needs to be true for now.
      {
        foreach (var handle in handles.GetEnumerator()) {
          if (handle.wasHeld && handle != _heldHandle) {
            if (_heldHandle != null) {
              _heldHandle.Release();
            }

            _heldHandle = handle;
          }
        }
      }

      // Make sure there's a delta pose entry for all currently attached handles.
      foreach (var handle in handles.GetEnumerator()) {
        if (!_objToHandleDeltaPoses.ContainsKey(handle)) {
          _objToHandleDeltaPoses[handle] = handle.pose.From(objPose);
        }
      }

      // Handle movement (easier when only one handle is held at any one time).
      if (_heldHandle != null) {
        // Move this object based on the movement of the held handle.
        var handleToObjPose = _objToHandleDeltaPoses[_heldHandle].inverse;
        var newObjPose = _heldHandle.pose.Then(handleToObjPose);

        this.targetPose = newObjPose;
      }

      updateMoveToTarget();

      // Move all handles to match the new pose of this object.
      foreach (var handle in handles.GetEnumerator()) {
        var objToHandlePose = _objToHandleDeltaPoses[handle];
        handle.targetPose = objPose.Then(objToHandlePose);
      }
    }

    public Action OnUpdateTarget = () => { };

    private void updateMoveToTarget() {
      OnUpdateTarget();

      pose = PhysicalInterfaceUtils.SmoothMove(prevPose, pose, targetPose);
    }

    #endregion

    #region IHandle

    public override Pose pose {
      get { return this.transform.ToPose(); }
      protected set {
        this.transform.SetPose(value);
      }
    }

    private Pose _targetPose;
    public Pose targetPose {
      get { return _targetPose; }
      set { _targetPose = value; }
    }

    public bool isHeld {
      get {
        return _heldHandle != null;
      }
    }

    public bool wasHeld {
      get {
        return handles.Query().Any(h => h.wasHeld);
      }
    }

    public bool wasMoved {
      get {
        return handles.Query().Any(h => h.wasMoved);
      }
    }

    public bool wasReleased {
      get {
        return handles.Query().Any(h => h.wasReleased);
      }
    }

    public bool wasThrown {
      get {
        return handles.Query().Any(h => h.wasReleased);
      }
    }

    public void Hold() {
      Debug.LogError("Can't hold a HandledObject directy; instead, call Hold() on one "
                     + "of one of its Handles.");
    }

    public void Release() {
      if (_heldHandle != null) {
        _heldHandle.Release();

        _heldHandle = null;
      }
    }

    #endregion

  }

  public struct TransformArrayComponentWrapper<GetComponentType>
                : IIndexable<GetComponentType>
  {
    Transform[] _arr;

    public TransformArrayComponentWrapper(Transform[] arr) {
      _arr = arr;
    }

    public GetComponentType this[int idx] {
      get { return _arr[idx].GetComponent<GetComponentType>(); }
    }

    public int Count { get { return _arr.Length; } }
  }

}
