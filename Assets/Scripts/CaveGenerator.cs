using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LibTessDotNet;
using UnityEngine;

public class CaveGenerator : MonoBehaviour
{
    /// <summary>
    ///     The integer seeding for the random.
    /// </summary>
    [SerializeField]
    private int _currentSeed = 1;

    /// <summary>
    ///     The maximum angle of a branch, positive as negative.
    /// </summary>
    [SerializeField]
    private float _maxBranchAngle = 60f;

    /// <summary>
    ///     The minimum angle between two branches.
    /// </summary>
    [SerializeField]
    private float _minimumAngleDifference = 15f;

    /// <summary>
    ///     The minimum distance in world-units between two nodes.
    /// </summary>
    [SerializeField]
    private float _minNodeDistance = 5f;

    /// <summary>
    ///     The maximum distance in world-units between two nodes.
    /// </summary>
    [SerializeField]
    private float _maxNodeDistance = 10f;

    /// <summary>
    ///     The maximum length of the graph.
    /// </summary>
    [SerializeField]
    private float _maxGraphLength = 100f;

    /// <summary>
    ///     The distribution of the amount of branches based on the distance.
    /// </summary>
    [SerializeField]
    private AnimationCurve _branchDistribution = null;

    /// <summary>
    ///     A scale component of the branch distribution.
    /// </summary>
    [SerializeField]
    private float _branchVariance = 1.5f;

    /// <summary>
    ///     The minimum distance in that branches are forced, meaning at least
    ///     one branch is guaranteed to continue.
    /// </summary>
    [SerializeField]
    private float _forceBranchDistance = 0.2f;

    /// <summary>
    ///     This denotes the maximum distance two intersecting points can have
    ///     for them to get merged instead of discarded.
    /// </summary>
    [SerializeField]
    private float _nodeMergeThreshold = 1f;

    /// <summary>
    ///     The width of a cave segment.
    /// </summary>
    [SerializeField]
    private float _caveWidth = 2f;

    /// <summary>
    ///     The individual overlap of segments.
    /// </summary>
    [SerializeField]
    private float _segmentOverlap = 0.5f;

    /// <summary>
    ///     The amount of vertices per line segment.
    /// </summary>
    [SerializeField]
    private int _verticesPerSegment = 10;

    [SerializeField]
    private Material _caveBackgroundMaterial = null;

    [SerializeField]
    private Material _caveForegroundMaterial = null;

    [SerializeField]
    private float _uvTiling = 5f;

    [SerializeField]
    private bool _regenerate = false;
    private bool _doRegenerate = false;

    [SerializeField]
    private int _iterationLimit = 1500;

    private readonly List<NodeConnection> _nodeConnections = new();
    private readonly List<CaveSegment> _segments = new();
    private readonly List<CaveHull> _hulls = new();

    private MeshFilter[] _meshes = null;
    private readonly Dictionary<MeshFilter, Vector3[]> _vertexCache = new();

    private GameObject _caveRoot = null;

    private void Start()
    {
        // We initialize the random with a given seed.
        Random.InitState(_currentSeed);
        GenerateCaveTree();
    }

    private void OnDisable()
    {
        DestroyImmediate(_caveRoot);
    }

    private void GenerateCaveTree()
    {
        DestroyImmediate(_caveRoot);
        _nodeConnections.Clear();
        _segments.Clear();
        _meshes = null;
        _hulls.Clear();
        _vertexCache.Clear();

        List<Vector2> currentNodes = new()
        {
            Vector2.zero,
        };

        while (currentNodes.Count != 0)
        {
            // We first pull the current origin we have to generate from.
            Vector2 currentOrigin = currentNodes[0];
            currentNodes.RemoveAt(0);

            // We measure the distance only X-based. We scale it to 0-1 based on the length.
            float xDelta = Mathf.Clamp01(currentOrigin.x / _maxGraphLength);

            // We then determine the amount of branches.
            int branches = GetNextBranchCount(xDelta);
            if (branches == 0)
            {
                // No branches no continue.
                continue;
            }

            // We note all the existing angles here.
            List<float> existingAngles = new(branches);

            // We pre-allocate the maximum amount of branches we're gonna have!
            List<Vector2> targetConnections = new(branches);
            bool hasGeneratedBranch = false;
            int attemptedCollidingChecks = 0;
            for (int i = 0; i < branches; i++)
            {
                // We generate a vector that has a length in between the min and max node distance
                // and also a random angle between the branch limits.
                Vector2 offsetDistance = Vector2.right * Random.Range(_minNodeDistance, _maxNodeDistance);
                float angle;

                // We have a maximum amount of attempts on an angle check of 10.
                int angleAttempt = 0;
                do
                {
                    angle = Random.Range(-_maxBranchAngle, _maxBranchAngle);
                    angleAttempt++;
                }
                while (HasConflictingAngle(existingAngles, angle) && angleAttempt < 10);

                // And then rotate the distance vector by that amount.
                Vector2 offsetVector = Rotate(offsetDistance, angle);
                Vector2 targetPosition = currentOrigin + offsetVector;

                // TODO determine if we can actually use this one.
                bool hasCollision = HasBranchCollision(
                    currentOrigin,
                    targetPosition,
                    out Vector2 intersectionTarget);
                if (hasCollision && (intersectionTarget - targetPosition).magnitude < _nodeMergeThreshold)
                {
                    hasCollision = false;
                    targetPosition = intersectionTarget;
                }

                if (hasCollision)
                {
                    // Okay this is a bit ugly. If we have a collision and we cannot resolve it then
                    // we need to check if we are beneath the forceBranchDistance and if so
                    // then we also need to check if this is the last generation attempt.
                    // If it is then we try again until we succeed, for a maximum of 10 times.
                    if (!hasGeneratedBranch
                        && xDelta < _forceBranchDistance
                        && i == branches - 1
                        && attemptedCollidingChecks < 10)
                    {
                        i--;
                        attemptedCollidingChecks++;
                    }

                    continue;
                }

                // We finally add the vector to our list or target connections.
                targetConnections.Add(targetPosition);
                existingAngles.Add(angle);
                currentNodes.Add(targetPosition);
                hasGeneratedBranch = true;
                attemptedCollidingChecks = 0;
            }

            _nodeConnections.Add(new NodeConnection
            {
                Origin = currentOrigin,
                Targets = targetConnections.ToArray(),
            });
        }

        // Finally, when we're done determining what nodes we're gonna have,
        // we need to rotate them around the maximum angle.
        RotateEntireTree();

        // After that we convert the node connections to their individual segments, on which
        // we can then perform the meshing.
        foreach (NodeConnection connection in _nodeConnections)
        {
            foreach (Vector2 target in connection.Targets)
            {
                _segments.Add(new CaveSegment
                {
                    Connection = connection,
                    Origin = connection.Origin,
                    Target = target,
                    Type = GetRandomType(),
                });
            }
        }

        // Then we use this information to generate the cave.
        GenerateCaveMesh();
    }

    /// <summary>
    ///     Generates the cave's mesh based on individual cave segments.
    /// </summary>
    /// <returns>True if the mesh hull generation succeeded.</returns>
    private bool GenerateCaveMesh()
    {
        _caveRoot = new("Cave");
        _meshes = new MeshFilter[_segments.Count];
        for (int i = 0; i < _segments.Count; i++)
        {
            CaveSegment segment = _segments[i];

            GameObject targetObject = new GameObject(i.ToString());
            targetObject.transform.parent = _caveRoot.transform;

            // Based on the segment we generate a specific shape.
            switch (segment.Type)
            {
                case SegmentType.Corridor:
                    _meshes[i] = GenerateCorridorSegment(_caveRoot, targetObject, segment);
                    break;
            }

            targetObject.SetActive(false);
        }

        // After creating the individual meshes we need to define a hull of our cave.
        // This is for one the entire outline of our cave and any other hole that the cave defines.
        // These holes can be simply filled with another mesh then.
        if (!BuildCaveHulls())
        {
            return false;
        }

        // We then generate all hulls first for the inner meshes
        GenerateInnerHullMeshes();
        GenerateOuterHullMesh();
        return true;
    }

    private void GenerateOuterHullMesh()
    {
        LineSegment[] segments = _hulls[0].Segments;

        ContourVertex[] contour = new ContourVertex[segments.Length + 4];
        contour[0].Position = new Vec3(-200, 0, 0);
        contour[1].Position = new Vec3(-200, -300, 0);
        contour[2].Position = new Vec3(300, -300, 0);
        contour[3].Position = new Vec3(300, 200, 0);

        for (int i = 0; i < segments.Length; i++)
        {
            contour[i + 4].Position = new Vec3(segments[i].Origin.x, segments[i].Origin.y, 0);
        }

        Tess tesselator = new();
        tesselator.AddContour(contour, ContourOrientation.CounterClockwise);
        tesselator.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        Vector3[] vertices = tesselator.Vertices.Select(x => new Vector3(x.Position.X, x.Position.Y)).ToArray();
        int[] triangles = tesselator.Elements;
        Vector2[] uvs = vertices.Select(vertex => new Vector2(vertex.x / _uvTiling, vertex.y / _uvTiling)).ToArray();

        Mesh targetMesh = new()
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs,
        };

        targetMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

        GameObject caveMeshPart = new GameObject("CaveOutside");
        caveMeshPart.transform.position = new Vector3(0, 0, 4.5f);
        caveMeshPart.transform.parent = _caveRoot.transform;

        MeshFilter meshFilter = caveMeshPart.AddComponent<MeshFilter>();
        meshFilter.mesh = targetMesh;

        caveMeshPart.AddComponent<MeshRenderer>().material = _caveForegroundMaterial;
    }

    private void GenerateInnerHullMeshes()
    {
        for (int i = 1; i < _hulls.Count; i++)
        {
            TesselateMesh(_hulls[i], _caveForegroundMaterial, 4.5f);
        }

        TesselateMesh(_hulls[0], _caveBackgroundMaterial, 5f);
    }

    private void TesselateMesh(in CaveHull hull, Material targetMaterial, float zLayer)
    {
        LineSegment[] segments = hull.Segments;

        ContourVertex[] contour = new ContourVertex[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            contour[i].Position = new Vec3(segments[i].Origin.x, segments[i].Origin.y, 0);
        }

        Tess tesselator = new();
        tesselator.AddContour(contour, ContourOrientation.CounterClockwise);
        tesselator.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        Vector3[] vertices = tesselator.Vertices.Select(x => new Vector3(x.Position.X, x.Position.Y)).ToArray();
        int[] triangles = tesselator.Elements;
        Vector2[] uvs = vertices.Select(vertex => new Vector2(vertex.x / _uvTiling, vertex.y / _uvTiling)).ToArray();

        Mesh targetMesh = new()
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs,
        };

        targetMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

        GameObject caveMeshPart = new GameObject("CaveMeshPart");
        caveMeshPart.transform.position = new Vector3(0, 0, zLayer);
        caveMeshPart.transform.parent = _caveRoot.transform;

        MeshFilter meshFilter = caveMeshPart.AddComponent<MeshFilter>();
        meshFilter.mesh = targetMesh;

        caveMeshPart.AddComponent<MeshRenderer>().material = targetMaterial;
    }

    private bool BuildCaveHulls()
    {
        // Very important here is that we make use of the order we define our mesh in.
        // The first half of the vertices are always the "top" side and the other ones
        // are the "bottom" side.

        HashSet<Vector3> checkedVertices = new();

        CaveHull outerHull = new();
        bool isValidHull = TraceCaveHull(checkedVertices, 0, 0, ref outerHull);
        if (!isValidHull)
        {
            // If we cannot build the outer hull then we have to try again. Any other hull doesn't matter.
            Debug.Log("Invalid hull!");
            return false;
        }

        _hulls.Add(outerHull);
        DiscardCollidingVertices(checkedVertices);

        int iterationMax = 200;
        Debug.ClearDeveloperConsole();

        int lastMeshIndex = 0;
        while (FindNextNonDiscardedVertex(checkedVertices, lastMeshIndex, out int filterIndex, out int vertexIndex))
        {
            iterationMax--;
            if (iterationMax == 0)
            {
                Debug.LogError("Hit hull generation iteration max!");
                break;
            }

            lastMeshIndex = filterIndex;

            CaveHull innerHull = new();
            isValidHull = TraceCaveHull(
                checkedVertices,
                filterIndex,
                vertexIndex,
                ref innerHull,
                isInnerCheck: true);
            if (!isValidHull)
            {
                continue;
            }

            _hulls.Add(innerHull);
        }

        Debug.Log("Found " + _hulls.Count + " valid hulls");
        return true;
    }

    /// <summary>
    ///     Discards any vertex that remains unchecked and collides with any other mesh.
    ///     This is used so we can easily trace the hull of holes only.
    ///     Thinking of it now, this could have been really useful to do beforehand in general.
    /// </summary>
    /// <param name="checkedVertices">The set of checked vertices.</param>
    private void DiscardCollidingVertices(HashSet<Vector3> checkedVertices)
    {
        int discarded = 0;
        for (int meshIndex = 0; meshIndex < _meshes.Length; meshIndex++)
        {
            MeshFilter targetFilter = _meshes[meshIndex];
            Vector3[] vertices = GetMeshVertices(targetFilter);
            foreach (Vector3 vertex in vertices)
            {
                if (checkedVertices.Contains(vertex))
                {
                    continue;
                }

                bool hasCollision = DoesCollideWithAnyMesh(vertex, targetFilter, out _, out _);
                if (!hasCollision)
                {
                    continue;
                }

                checkedVertices.Add(vertex);
                discarded++;
            }
        }
    }

    /// <summary>
    ///     Finds the first non-discarded vertex in the entire tree. Returns false if there are none.
    /// </summary>
    /// <param name="checkedVertices">The set of all checked vertices.</param>
    /// <param name="startFilterIndex">The index to start the search on.</param>
    /// <param name="filterIndex">The index of the filter with the first non-discarded vertex.</param>
    /// <param name="vertexIndex">The index of the vertex in the mesh's vertices.</param>
    /// <returns>True if a non-discarded vertex has been found.</returns>
    private bool FindNextNonDiscardedVertex(
        HashSet<Vector3> checkedVertices,
        int startFilterIndex,
        out int filterIndex,
        out int vertexIndex)
    {
        for (int meshIndex = startFilterIndex; meshIndex < _meshes.Length; meshIndex++)
        {
            Vector3[] vertices = GetMeshVertices(_meshes[meshIndex]);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (checkedVertices.Contains(vertices[i]))
                {
                    continue;
                }

                // We have found a vertex we can use!
                filterIndex = meshIndex;
                vertexIndex = i;
                return true;
            }
        }

        filterIndex = -1;
        vertexIndex = -1;
        return false;
    }

    private bool TraceCaveHull(
        HashSet<Vector3> checkedVertices,
        int startFilterIndex,
        int vertexIndex,
        ref CaveHull hull,
        bool isInnerCheck = false)
    {
        List<LineSegment> hullSegments = new();

        int currentFilterIndex = startFilterIndex;
        MeshFilter currentFilter = _meshes[currentFilterIndex];
        Vector3[] currentVertices = GetMeshVertices(currentFilter);

        Vector3 lastValidVertex = currentVertices[vertexIndex];
        Vector3 startVector = lastValidVertex;
        int currentVertexIndex = vertexIndex;

        int iterationLimit = _iterationLimit;
        while (true)
        {
            iterationLimit--;
            if (iterationLimit < 0)
            {
                Debug.LogError("Iteration limit hit.");
                break;
            }

            // Note that we start with a valid vertex. We attempt to go to the next vertex here.
            bool wasTopVertex = currentVertexIndex < currentVertices.Length / 2;
            if (wasTopVertex && currentVertexIndex + 1 >= currentVertices.Length / 2)
            {
                // This would mean that we switch from top to bottom here inside our mesh.
                // I.e. we need to find the mesh to our right!
                int nextSegment = FindTopRightSegment(lastValidVertex, currentFilterIndex);
                if (nextSegment == -1)
                {
                    // We skip to the last index simply.
                    currentVertexIndex = currentVertices.Length - 1;
                }
                else
                {
                    currentFilterIndex = nextSegment;
                    currentFilter = _meshes[nextSegment];
                    currentVertices = GetMeshVertices(currentFilter);

                    currentVertexIndex = GetFirstNonCollidingVertex(
                        lastValidVertex,
                        currentFilterIndex,
                        currentVertices,
                        true);
                }
            }
            else if (!wasTopVertex && currentVertexIndex - 1 < currentVertices.Length / 2)
            {
                // This would mean that we switch from bottom to top here inside our mesh.
                // I.e. we need to find the mesh to our left!
                int nextSegment = FindBottomLeftSegment(lastValidVertex, currentFilterIndex);
                if (nextSegment == -1)
                {
                    // We skip to the first index simply.
                    currentVertexIndex = 0;
                }
                else
                {
                    currentFilterIndex = nextSegment;
                    currentFilter = _meshes[nextSegment];
                    currentVertices = GetMeshVertices(currentFilter);

                    currentVertexIndex = GetFirstNonCollidingVertex(
                        lastValidVertex,
                        currentFilterIndex,
                        currentVertices,
                        false);
                }
            }
            else
            {
                if (wasTopVertex)
                {
                    // We're going to the right here!
                    currentVertexIndex++;
                }
                else
                {
                    // If we're going through the bottom then we're
                    // going to the left!
                    currentVertexIndex--;
                }
            }

            if (currentVertexIndex > currentVertices.Length || currentVertexIndex < 0)
            {
                Debug.LogError("Got illegal vertex");
                break;
            }

            Vector3 targetVertex = currentVertices[currentVertexIndex];
            if (!isInnerCheck && checkedVertices.Contains(targetVertex))
            {
                // We've hit a known vertex and have constructed the whole hull here!
                break;
            }

            if (!isInnerCheck)
            {
                checkedVertices.Add(targetVertex);
            }

            Vector3 oldVertex = targetVertex;
            DecideCollisionBranch(
                ref targetVertex,
                ref currentFilterIndex,
                ref currentFilter,
                ref currentVertices,
                ref currentVertexIndex,
                checkedVertices,
                lastValidVertex);

            if (oldVertex != targetVertex)
            {
                checkedVertices.Remove(oldVertex);
                checkedVertices.Add(targetVertex);
            }
            else if (isInnerCheck && checkedVertices.Contains(targetVertex))
            {
                // We've hit a known vertex and have constructed the whole hull here!
                break;
            }

            if (isInnerCheck)
            {
                checkedVertices.Add(targetVertex);
            }

            // We add the line to our hull.
            hullSegments.Add(new LineSegment
            {
                Origin = lastValidVertex,
                Target = targetVertex,
            });

            lastValidVertex = targetVertex;
            if (targetVertex == startVector)
            {
                break;
            }
        }

        checkedVertices.Add(startVector);
        checkedVertices.Add(lastValidVertex);

        hull.Segments = hullSegments.ToArray();
        return lastValidVertex == startVector && hull.Segments.Length > 1;
    }

    private void DecideCollisionBranch(
        ref Vector3 targetVertex,
        ref int currentFilterIndex,
        ref MeshFilter currentFilter,
        ref Vector3[] currentVertices,
        ref int currentVertexIndex,
        HashSet<Vector3> checkedVertices,
        Vector3 lastValidVertex)
    {
        bool isTopSide = currentVertexIndex < currentVertices.Length / 2;
        Vector3 originalVertex = targetVertex;
        int originalFilterIndex = currentFilterIndex;
        MeshFilter originalFilter = currentFilter;

        // We now check for a mesh collision here.
        bool hasCollision = DoesCollideWithAnyMesh(
            targetVertex,
            currentFilter,
            out int collisionIndex,
            out int triIndex);

        // This is to not count bounces
        HashSet<Vector3> collisionCheckedVertices = new()
        {
            targetVertex,
            lastValidVertex,
        };

        HashSet<Vector3> possibleRollbacks = new();

        int maxChecks = 1;
        while (hasCollision && maxChecks != 0)
        {
            // We actually do have a mesh collision. We now switch to the next mesh based
            // on the tri we hit!
            currentFilterIndex = collisionIndex;
            currentFilter = _meshes[collisionIndex];
            currentVertices = GetMeshVertices(currentFilter);

            int targetVertexIndex = currentFilter.sharedMesh.triangles[3 * triIndex];
            if (DoesCollideWithAnyMesh(currentVertices[targetVertexIndex], currentFilter, out _, out _))
            {
                targetVertexIndex = currentFilter.sharedMesh.triangles[3 * triIndex + 1];
            }

            bool isTargetTop = targetVertexIndex < currentVertices.Length / 2;

            // We check if we should switch based on distance here again.
            int vertexToTest = currentFilter.sharedMesh.triangles[3 * triIndex + 2];

            float currentDistance = (currentVertices[targetVertexIndex] - lastValidVertex).magnitude;
            float testDistance = (currentVertices[vertexToTest] - lastValidVertex).magnitude;
            if (testDistance < currentDistance)
            {
                // We have a mismatch. We either were top and the target is bottom
                // or the other way around. Usually these do match well, but not here.
                targetVertexIndex = vertexToTest;
                isTargetTop = targetVertexIndex < currentVertices.Length / 2;
            }

            currentVertexIndex = targetVertexIndex;
            targetVertex = currentVertices[currentVertexIndex];
            if (checkedVertices.Contains(targetVertex))
            {
                currentVertexIndex += isTargetTop ? 1 : -1;
                targetVertex = currentVertices[currentVertexIndex];
            }

            if (collisionCheckedVertices.Contains(targetVertex))
            {
                // We have detected a bounce and are continuing with the next vertex in line.
                // Now we have to check if we do an illegal rollover

                int oldIndex = currentVertexIndex;
                currentVertexIndex += isTargetTop ? 1 : -1;
                if (currentVertexIndex < currentVertices.Length / 2 && oldIndex >= currentVertices.Length / 2
                    || oldIndex < currentVertices.Length / 2 && currentVertexIndex >= currentVertices.Length / 2)
                {
                    currentVertexIndex = oldIndex;
                }
                else
                {
                    targetVertex = currentVertices[currentVertexIndex];
                }
            }

            if (checkedVertices.Contains(targetVertex))
            {
                maxChecks = 0;
                break;
            }

            if (!checkedVertices.Contains(targetVertex))
            {
                possibleRollbacks.Add(targetVertex);
            }

            collisionCheckedVertices.Add(targetVertex);
            maxChecks--;
            hasCollision = DoesCollideWithAnyMesh(
                targetVertex,
                currentFilter,
                out collisionIndex,
                out triIndex);
        }

        if (maxChecks != 0)
        {
            return;
        }

        foreach (Vector3 rollback in possibleRollbacks)
        {
            checkedVertices.Remove(rollback);
        }

        // We now get all colliding meshes
        GetCollidingMeshes(
            originalVertex,
            originalFilter,
            out int[] collidingMeshes);

        // And add their vertex data stuff to a list!
        List<(int meshIndex, Vector3 vertex, int vertexIndex)> meshData = new();
        foreach (int collidingMesh in collidingMeshes)
        {
            MeshFilter collidedFilter = _meshes[collidingMesh];
            Vector3[] localVertices = GetMeshVertices(collidedFilter);
            for (int vertex = 0; vertex < localVertices.Length; vertex++)
            {
                meshData.Add((collidingMesh, localVertices[vertex], vertex));
            }
        }

        // We only prefer the same side if we have only a single colliding mesh and this is a straight branch
        bool shouldPreferSameSide = collidingMeshes.Length == 1 && _segments[originalFilterIndex].Connection.Targets.Length == 1;

        // Then we sort the array by distance
        (int meshIndex, Vector3 vertex, int vertexIndex)[] vertices = meshData.ToArray();
        System.Array.Sort(vertices, (a, b) =>
        {
            return (int)Mathf.Sign((a.vertex - lastValidVertex).magnitude - (b.vertex - lastValidVertex).magnitude);
        });

        foreach ((int meshIndex, Vector3 vertex, int vertexIndex) in vertices)
        {
            // Skip any checked vertices
            if (vertex == originalVertex || checkedVertices.Contains(vertex))
            {
                continue;
            }

            MeshFilter collidedFilter = _meshes[meshIndex];
            if (shouldPreferSameSide && isTopSide != (vertexIndex < collidedFilter.sharedMesh.vertexCount / 2))
            {
                // We skip any vertex not being on the same side here.
                continue;
            }

            // Check here if we collide with anything
            if (DoesCollideWithAnyMesh(
                vertex,
                collidedFilter,
                out _,
                out _))
            {
                continue;
            }

            // If not, this is our targeted vertex!
            currentFilterIndex = meshIndex;
            currentFilter = collidedFilter;
            currentVertices = GetMeshVertices(currentFilter);

            targetVertex = vertex;
            currentVertexIndex = System.Array.IndexOf(currentVertices, vertex);
            return;
        }
    }

    private int GetFirstNonCollidingVertex(
        Vector3 originVertex,
        int originFilterIndex,
        Vector3[] vertices,
        bool isTopVertex)
    {
        int startIndex = isTopVertex ? 0 : vertices.Length - 1;
        int endIndex = vertices.Length / 2;
        int modifier = isTopVertex ? 1 : -1;

        System.Func<int, bool> condition;
        if (isTopVertex)
        {
            condition = vertexIndex => vertexIndex < endIndex;
        }
        else
        {
            condition = vertexIndex => vertexIndex >= endIndex;
        }

        for (int vertexIndex = startIndex; condition(vertexIndex); vertexIndex += modifier)
        {
            // We iterate through the top or bottom half from either left-to-right or right-to-left
            // and find the first vertex that does not collide!
            Vector3 targetVertex = vertices[vertexIndex];
            if (DoesCollideWithAnyMesh(targetVertex, _meshes[originFilterIndex], out _, out _))
            {
                continue;
            }

            // We also should check if this is an inversion, meaning if we're going left for top
            // vertices, or right for bottom vertices
            if (IsInvertedDirection(isTopVertex, originVertex, targetVertex, originFilterIndex))
            {
                continue;
            }

            return vertexIndex;
        }

        Debug.LogError("ALL VERTICES COLLIDE?!");
        return -1;
    }

    private bool IsInvertedDirection(
        bool isTopVertex,
        Vector2 originVertex,
        Vector2 targetVertex,
        int originFilterIndex)
    {
        CaveSegment targetSegment = _segments[originFilterIndex];
        Vector2 segmentDirection = isTopVertex ? (targetSegment.Target - targetSegment.Origin) : (targetSegment.Origin - targetSegment.Target);
        Vector2 targetDirection = targetVertex - originVertex;

        return Vector2.Angle(segmentDirection, targetDirection) > 160;
    }

    private bool DoesCollideWithAnyMesh(
        Vector3 vertex,
        MeshFilter originFilter,
        out int collisionIndex,
        out int triIndex)
    {
        // We go through all the meshes here first.
        for (int meshIndex = 0; meshIndex < _meshes.Length; meshIndex++)
        {
            // We know that the meshes and cave segments share their indices.
            // We can compare distances here to filter out far away meshes.
            Vector3 meshOrigin = _segments[meshIndex].Origin;
            if ((meshOrigin - vertex).magnitude > 4 * _maxNodeDistance)
            {
                // We filter them out here based on a very lenient distance
                // since we compare from the mesh's origin.
                continue;
            }

            if (_meshes[meshIndex] == originFilter)
            {
                // Important as well is that we actually skip our own mesh.
                // We do not want to self-intersect.
                continue;
            }

            Mesh targetMesh = _meshes[meshIndex].sharedMesh;
            Vector3[] vertices = GetMeshVertices(_meshes[meshIndex]);
            int[] tris = targetMesh.triangles;

            for (int triangle = 0; triangle < tris.Length / 3; triangle++)
            {
                // We now go through all triangles in the mesh here and check for a collision!
                bool isContained = IsInTri(
                    vertex,
                    vertices[tris[3 * triangle + 0]],
                    vertices[tris[3 * triangle + 1]],
                    vertices[tris[3 * triangle + 2]]);
                if (!isContained)
                {
                    continue;
                }

                // We now have a collision with this mesh here.
                collisionIndex = meshIndex;
                triIndex = triangle;
                return true;
            }
        }

        collisionIndex = -1;
        triIndex = -1;
        return false;
    }

    private void GetCollidingMeshes(
        Vector3 vertex,
        MeshFilter originFilter,
        out int[] collisionIndices)
    {
        List<int> collidingMeshes = new();

        // We go through all the meshes here first.
        for (int meshIndex = 0; meshIndex < _meshes.Length; meshIndex++)
        {
            // We know that the meshes and cave segments share their indices.
            // We can compare distances here to filter out far away meshes.
            Vector3 meshOrigin = _segments[meshIndex].Origin;
            if ((meshOrigin - vertex).magnitude > 4 * _maxNodeDistance)
            {
                // We filter them out here based on a very lenient distance
                // since we compare from the mesh's origin.
                continue;
            }

            if (_meshes[meshIndex] == originFilter)
            {
                // Important as well is that we actually skip our own mesh.
                // We do not want to self-intersect.
                continue;
            }

            Mesh targetMesh = _meshes[meshIndex].sharedMesh;
            Vector3[] vertices = GetMeshVertices(_meshes[meshIndex]);
            int[] tris = targetMesh.triangles;

            for (int triangle = 0; triangle < tris.Length / 3; triangle++)
            {
                // We now go through all triangles in the mesh here and check for a collision!
                bool isContained = IsInTri(
                    vertex,
                    vertices[tris[3 * triangle + 0]],
                    vertices[tris[3 * triangle + 1]],
                    vertices[tris[3 * triangle + 2]]);
                if (!isContained)
                {
                    continue;
                }

                // We now have a collision with this mesh here. We immediately break so we skip
                // to the next mesh!
                collidingMeshes.Add(meshIndex);
                break;
            }
        }

        collisionIndices = collidingMeshes.ToArray();
    }

    private int FindTopRightSegment(Vector2 originVertex, int segmentIndex)
    {
        Vector2 targetOrigin = _segments[segmentIndex].Target;

        // We need to find all segments where the target is its origin
        List<int> connectingSegments = new();
        for (int i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].Origin != targetOrigin)
            {
                continue;
            }

            connectingSegments.Add(i);
        }

        if (connectingSegments.Count == 0)
        {
            return -1;
        }
        else if (connectingSegments.Count == 1)
        {
            return connectingSegments[0];
        }

        int closestIndex = -1;
        float closestDistance = int.MaxValue;

        foreach (int index in connectingSegments)
        {
            Mesh targetMesh = _meshes[index].sharedMesh;
            Vector3[] vertices = GetMeshVertices(_meshes[index]);
            for (int vertex = 0; vertex < vertices.Length / 2; vertex++)
            {
                // We're going through the top half of the vertices here to find our next one!
                if (DoesCollideWithAnyMesh(vertices[vertex], _meshes[index], out _, out _))
                {
                    // We skip any colliding vertices.
                    continue;
                }

                Vector2 targetVertex = vertices[vertex];
                float distance = (targetVertex - originVertex).magnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private int FindBottomLeftSegment(Vector2 originVertex, int segmentIndex)
    {
        Vector2 targetOrigin = _segments[segmentIndex].Origin;

        // There should be only one segment this connects to!
        for (int i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].Target != targetOrigin)
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>
    ///     Generates a cave segment that is a corridor.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <param name="segment">The segment to base this on.</param>
    private MeshFilter GenerateCorridorSegment(GameObject root, GameObject target, in CaveSegment segment)
    {
        // We add the filter and renderer here.
        MeshFilter filter = target.AddComponent<MeshFilter>();
        MeshRenderer renderer = target.AddComponent<MeshRenderer>();

        // Direction of the line.
        Vector2 direction = (segment.Target - segment.Origin).normalized;
        // Normal of the line. Is already normalized.
        Vector2 normal = new(-direction.y, direction.x);

        // Segment start and end position, including overlap.
        Vector2 segmentStart = segment.Origin - direction * _segmentOverlap;
        Vector2 segmentEnd = segment.Target + direction * _segmentOverlap;

        // A vector describing the offset from the start segment when multiplied
        // with a value between 0 and 1.
        Vector2 lineVector = segmentEnd - segmentStart;

        // This curve describes the shape of our corridor.
        AnimationCurve corridorCurve = new(
            new Keyframe
            {
                time = 0,
                value = 0,
            },
            new Keyframe
            {
                time = 1 / 3f,
                value = Random.Range(-1f, 1f),
            },
            new Keyframe
            {
                time = 2 / 3f,
                value = Random.Range(-1f, 1f),
            },
            new Keyframe
            {
                time = 1f,
                value = 0f,
            });

        // We create our segments.
        Vector3[] vertices = new Vector3[2 * _verticesPerSegment];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int vertex = 0; vertex < _verticesPerSegment; vertex++)
        {
            Vector2 directionOffset = segmentStart + lineVector * (vertex / (_verticesPerSegment - 1f));
            Vector2 normalOffset = normal * corridorCurve.Evaluate(vertex / (_verticesPerSegment - 1f)) * _caveWidth;

            vertices[vertex] = directionOffset + (normal + normalOffset) * (_caveWidth / 2f);
            vertices[vertex + _verticesPerSegment] = directionOffset - (normal - normalOffset) * (_caveWidth / 2f);

            uvs[vertex] = vertices[vertex] / _uvTiling;
            uvs[vertex + _verticesPerSegment] = vertices[vertex + _verticesPerSegment] / _uvTiling;
        }

        // The amount of tris is defined as 2 * (segments - 1), and 3 tri indices per tri.
        int[] triangles = new int[6 * (_verticesPerSegment - 1)];
        for (int quad = 0; quad < _verticesPerSegment - 1; quad++)
        {
            int quadTopIndex = quad;
            int quadBottomIndex = quad + _verticesPerSegment;

            triangles[6 * quad + 0] = quadTopIndex;
            triangles[6 * quad + 1] = quadTopIndex + 1;
            triangles[6 * quad + 2] = quadBottomIndex + 1;
            triangles[6 * quad + 3] = quadBottomIndex + 1;
            triangles[6 * quad + 4] = quadBottomIndex;
            triangles[6 * quad + 5] = quadTopIndex;
        }

        Mesh targetMesh = new()
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs,
        };

        // renderer.material = _caveBackgroundMaterial;
        filter.sharedMesh = targetMesh;
        return filter;
    }

    /// <summary>
    ///     Rotates the entire generated tree so that it goes beneath ground.
    /// </summary>
    private void RotateEntireTree()
    {
        // We determine the highest angle of rotation between the x-axis.
        // We do this for the origins only.
        float rotationAngle = float.MinValue;
        foreach (NodeConnection connection in _nodeConnections)
        {
            float angle = Vector2.Angle(Vector2.right, connection.Origin);
            if (angle < rotationAngle)
            {
                continue;
            }

            rotationAngle = angle;
        }

        if (rotationAngle <= 0)
        {
            // We do not rotate if the highest angle is beneath 0, that is fine. Above
            // means we do need to rotate the cave below ground.
            return;
        }

        // Rotate any origin and target by -rotationAngle!
        foreach (NodeConnection connection in _nodeConnections)
        {
            connection.Origin = Rotate(connection.Origin, -rotationAngle);
            for (int i = 0; i < connection.Targets.Length; i++)
            {
                connection.Targets[i] = Rotate(connection.Targets[i], -rotationAngle);
            }
        }
    }

    private bool HasConflictingAngle(List<float> angles, float targetAngle)
    {
        foreach (var angle in angles)
        {
            if (Mathf.Abs(targetAngle - angle) >= _minimumAngleDifference)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasBranchCollision(
        Vector2 origin,
        Vector2 target,
        out Vector2 intersectionTarget)
    {
        List<(NodeConnection Connection, Vector2 IntersectionPoint)> intersections = new();

        // We go through all existing connections here.
        foreach (NodeConnection connection in _nodeConnections)
        {
            // If the given node connection's origin plus the max node distance is less than
            // our distance then we skip the collision check. We only check "right" of the origin.
            // We add a leniance of 2x to this.
            if (connection.Origin.x + 2f * _maxNodeDistance <= origin.x)
            {
                continue;
            }
            else if (origin.x + 2f * _maxNodeDistance < connection.Origin.x)
            {
                // This also works for nodes to the right of this one.
                continue;
            }

            // We then go through all targets of this connection and check for a collision.
            foreach (var connectionTarget in connection.Targets)
            {
                // If we originate from the given target then we skip this check.
                if (connectionTarget == origin)
                {
                    continue;
                }

                bool intersects = GetLineIntersection(
                    origin,
                    target,
                    connection.Origin,
                    connectionTarget,
                    out Vector2 intersectionPoint);
                if (!intersects)
                {
                    // If we do not intersect then we also do not care.
                    continue;
                }

                // If we do then we store this intersectionPoint in our list of intersections.
                intersections.Add((connection, intersectionPoint));
            }
        }

        // If we have no stored intersections then we immediately return.
        if (intersections.Count == 0)
        {
            intersectionTarget = Vector2.zero;
            return false;
        }
        else if (intersections.Count == 1)
        {
            // If we have a singular intersection then we also immediately return it.
            intersectionTarget = intersections[0].IntersectionPoint;
            return true;
        }

        // Since we are on a straight line, we can just check by distance of intersection points
        // which nodeConnection is closest to us. We return the nearest intersection of these.
        // We calculate these based off our _origin_, not our target value.
        float minDistance = float.MaxValue;
        int minIndex = -1;
        for (int i = 0; i < intersections.Count; i++)
        {
            // We calculate the intersection distance.
            var recordedIntersection = intersections[i];
            float intersectionDistance = (recordedIntersection.IntersectionPoint - origin).magnitude;
            if (intersectionDistance > minDistance)
            {
                continue;
            }

            minDistance = intersectionDistance;
            minIndex = i;
        }

        intersectionTarget = intersections[minIndex].IntersectionPoint;
        return true;
    }

    /// <summary>
    ///     Intersects two 2d lines and outputs the intersection point, if any.
    /// </summary>
    /// <param name="origin1">The origin of the first line.</param>
    /// <param name="target1">The target of the first line.</param>
    /// <param name="origin2">The origin of the second line.</param>
    /// <param name="target2">The target of the second line.</param>
    /// <param name="intersectionPoint">The intersection point, if any.</param>
    /// <returns>True if an intersection exists.</returns>
    private bool GetLineIntersection(
        in Vector2 origin1,
        in Vector2 target1,
        in Vector2 origin2,
        in Vector2 target2,
        out Vector2 intersectionPoint)
    {
        Vector2 s1 = new(target1.x - origin1.x, target1.y - origin1.y);
        Vector2 s2 = new(target2.x - origin2.x, target2.y - origin2.y);

        float scale = -s2.x * s1.y + s1.x * s2.y;
        float s = (-s1.y * (origin1.x - origin2.x) + s1.x * (origin1.y - origin2.y)) / scale;
        float t = (s2.x * (origin1.y - origin2.y) - s2.y * (origin1.x - origin2.x)) / scale;
        if (s < 0 || s > 1 || t < 0 || t > 1)
        {
            intersectionPoint = Vector2.zero;
            return false;
        }

        intersectionPoint = new Vector2(origin1.x + t * s1.x, origin1.y + t * s1.y);
        return true;
    }

    /// <summary>
    ///     Gets the amount of branches to generate next for the given xDelta of the point.
    /// </summary>
    /// <param name="xDelta">The delta from the origin, between 0 and 1.</param>
    /// <returns>An amount of branches to generate.</returns>
    private int GetNextBranchCount(float xDelta)
    {
        if (xDelta == 0)
        {
            // Special case: If we're at the origin then we have exactly one branch!
            return 1;
        }

        // We then evaluate the average branch value for this moment. This is the variance
        // multiplied by our distribution curve.
        float averageBranchValue = _branchVariance * _branchDistribution.Evaluate(xDelta);
        if (averageBranchValue == 0)
        {
            // No branches? No continue.
            return 0;
        }

        // We generate a random value between 0 and averageBranchValue.
        float actualBranches = Random.Range(0, averageBranchValue);
        int branches;
        if (actualBranches < 0.333f)
        {
            // If it's beneath a cutoff value then we default to 0.
            branches = 0;
        }
        else
        {
            // Otherwise we round up to the nearest integer.
            branches = Mathf.CeilToInt(actualBranches);
        }

        if (xDelta < _forceBranchDistance && branches == 0)
        {
            // If we're forcing the branch distance and the branch count is zero then we
            // fix it to 1.
            return 1;
        }

        return branches;
    }

    /// <summary>
    ///     Gets the next segment's type. This is a random type,
    ///     as defined in the enum itself.
    /// </summary>
    /// <returns>A random segment type.</returns>
    private SegmentType GetRandomType()
    {
        int rawType = Random.Range(0, 101);
        SegmentType targetType = SegmentType.None;
        foreach (object type in System.Enum.GetValues(typeof(SegmentType)))
        {
            // Okay fam, this is ugly but it has to be done like this :(
            if (rawType > (int)(SegmentType)type)
            {
                break;
            }

            targetType = (SegmentType)type;
        }

        return targetType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3[] GetMeshVertices(MeshFilter filter)
    {
        if (_vertexCache.TryGetValue(filter, out Vector3[] vertices))
        {
            return vertices;
        }

        vertices = filter.sharedMesh.vertices;
        _vertexCache.Add(filter, vertices);
        return vertices;
    }

    private static Vector2 Rotate(Vector2 vec, float angle)
    {
        return Quaternion.AngleAxis(angle, Vector3.forward) * vec;
    }

    private static float TriSign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    /// <summary>
    ///     Checks whether or not a given point is within a triangle.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <param name="v1">The first triangle point.</param>
    /// <param name="v2">The second triangle point.</param>
    /// <param name="v3">The third triangle point.</param>
    /// <returns>True if it is contained in the given traingle.</returns>
    private static bool IsInTri(Vector2 point, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float sign1 = TriSign(point, v1, v2);
        float sign2 = TriSign(point, v2, v3);
        float sign3 = TriSign(point, v3, v1);

        bool hasNegative = sign1 < 0 || sign2 < 0 || sign3 < 0;
        bool hasPositive = sign1 > 0 || sign2 > 0 || sign3 > 0;
        return !(hasNegative && hasPositive);
    }

    private class NodeConnection
    {
        public Vector2 Origin { get; set; }

        public Vector2[] Targets { get; set; }
    }

    /// <summary>
    ///     This enum defines segment types of a cave segment.
    ///     The values determine probabilities and must end up
    ///     at 100.
    /// </summary>
    private enum SegmentType
    {
        None = -1,
        Corridor = 100,
    }

    private struct CaveSegment
    {
        public NodeConnection Connection { get; set; }

        public Vector2 Origin { get; set; }

        public Vector2 Target { get; set; }

        public SegmentType Type { get; set; }
    }

    private struct CaveHull
    {
        public LineSegment[] Segments { get; set; }
    }

    private struct LineSegment
    {
        public Vector2 Origin { get; set; }

        public Vector2 Target { get; set; }
    }
}
