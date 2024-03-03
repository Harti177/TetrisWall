using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;

public class GamePlay : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject cubePrefab;

    private GamePiece[,] array = new GamePiece[0,0];
    private int xLength = 0;
    private int yLength = 0;

    private float time = 0f;
    private float playTime = 1f;

    private int score = 0; 
    private int lines = 0;
    private int level = 0; 

    private Piece currentPiece;

    private Move currentMove;

    private bool initialised = false;
    private bool gameStarted = false;
    private bool stepReached = false;
    private bool endPressed = false;
    private bool gamePaused = false;
    private bool piecePlacedDown = false;

    private bool sceneQuerySuccessful = false;
    private int height;
    private Dictionary<OVRScenePlane, List<GameObject>> wallCubes = new Dictionary<OVRScenePlane, List<GameObject>>();
    List<OVRScenePlane> _walls = new List<OVRScenePlane>();
    private Comparison<OVRScenePlane> _wallOrderComparer;
    private readonly Dictionary<Guid, int> _orderedRoomGuids = new Dictionary<Guid, int>();
    internal HashSet<Guid> _uuidToQuery = new HashSet<Guid>();

    Array PieceTypeValues = Enum.GetValues(typeof(PieceType));

    private bool fixedGamePlay = false;
    [SerializeField] private GameObject fixedGamePlayObject;

    [SerializeField] private bool useSample; 
    [SerializeField] private GameObject sampleObject; 

    #region UI
    [SerializeField] private GameObject loadingText;
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject fixedGamePlayButton;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject resetButton;
    [SerializeField] private GameObject endButton;
    [SerializeField] private TextMeshPro pauseResumeText;
    [SerializeField] private TextMeshPro loadingTextText;
    #endregion UI

    [SerializeField] private Transform interactorFollowPiece; 
    [SerializeField] private Transform interactorFollowCamera; 
    private Transform interactor = null; 

    // Start is called before the first frame update
    void Awake()
    {
        _wallOrderComparer = (planeA, planeB) =>
        {
            bool TryGetUuid(OVRScenePlane plane, out int index)
            {
                var guid = plane.GetComponent<OVRSceneAnchor>().Uuid;
                if (_orderedRoomGuids.TryGetValue(guid, out index)) return true;

                return false;
            }

            if (!TryGetUuid(planeA, out var indexA)) return 0;
            if (!TryGetUuid(planeB, out var indexB)) return 0;

            return indexA.CompareTo(indexB);
        };
    }

    // Update is called once per frame
    void Update()
    {
        //Game Paused
        if (gamePaused)
        {
            interactor.gameObject.SetActive(false);
            return; 
        }

        //Move time
        time += Time.deltaTime;

        //End game
        if(gameStarted && endPressed)
        {
            gameStarted = false;
            endPressed = false;

            EndGame(); 
            return; 
        }

        //Game Play
        if (gameStarted)
        {
            level = (lines / 4);
            //Piece initialization
            if (piecePlacedDown || currentPiece == null)
            {
                if(currentPiece != null) FillBox(currentPiece.values, true);

                if (piecePlacedDown)
                {
                    int linesThisLevel = 0; 
                    for (int j = 0; j < array.GetLength(1); j++)
                    {
                        bool notFilled = false; 
                        for (int i = 0; i < array.GetLength(0); i++)
                        {
                            if (!CheckBox(i, j)) notFilled = true;

                            if (notFilled)
                            {
                                break;
                            }

                        }
                        if (!notFilled)
                        {
                            linesThisLevel++;
                            for (int b = j; b < array.GetLength(1); b++)
                            {
                                for (int a = 0; a < array.GetLength(0); a++)
                                {
                                    EmptyBox(a, b);

                                    if(b != array.GetLength(1) - 1)
                                    {
                                        if(CheckBox(a, b + 1))
                                        {
                                            FillBox(a, b, true, true);
                                        }
                                    }
                                }
                            }

                            j--;
                        }
                    }
                    
                    if(linesThisLevel > 0)
                        score += (linesThisLevel + (2 * (linesThisLevel - 1))) * 100;
                    loadingTextText.text = "Score " + score.ToString(); 
                }

                int xPositionForNext = -1;
                int yPositionForNext = -1;

                if (piecePlacedDown)
                {
                    (xPositionForNext, yPositionForNext) = GetCentrePosition(); 
                }

                if (xPositionForNext == -1) xPositionForNext = UnityEngine.Random.Range(0, array.GetLength(0));
                yPositionForNext = array.GetLength(1);

                piecePlacedDown = false;

                playTime = 0.75f + (level * 0.1f);
                currentMove = null;

                currentPiece = new Piece();
                currentPiece.pieceType = (PieceType)PieceTypeValues.GetValue(UnityEngine.Random.Range(0, 28));

                currentPiece.color = colors[UnityEngine.Random.Range(0, colors.Length)];

                currentPiece.yPosition = yPositionForNext;
                currentPiece.xPosition = xPositionForNext;
            }

            if (currentMove != null && currentPiece.yPosition >= yLength)
            {
                currentMove = null;
            }

            //For now no move and step down at the same frame 
            if (currentMove == null)
            {
                stepReached = time > playTime ? true : false;

                if (stepReached)
                {
                    time = 0f;
                }
            }

            //Bring the piece down fast. Later a way to stop moving the piece fast 
            if (currentMove != null && currentMove.moveType == MoveType.down)
            {
                playTime = 0.05f;
                currentMove = null;
            }

            int[,] newPieceValues = null;

            PieceType pieceType = currentPiece.pieceType;
            Debug.Log(pieceType);
            switch (pieceType)
            {
                case PieceType.IBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = IBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true; 
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = IBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = IBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = IBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 1);

                            if (blocked)
                            {
                                newPieceValues = null; 
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.IBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.IBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = IBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = IBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = IBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = IBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 2);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.IBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.IBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = IBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = IBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = IBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = IBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 3);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.IBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.IBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = IBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = IBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = IBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = IBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0) - 1, array.GetLength(1) - 1, 4);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.IBlock1;
                            }
                        }
                        break;
                    }
                case PieceType.OBlock1:
                case PieceType.OBlock2:
                case PieceType.OBlock3: 
                case PieceType.OBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = OBlock.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = OBlock.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = OBlock.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        break; 
                    }
                case PieceType.TBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = TBlock1.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = TBlock1.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = TBlock1.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = TBlock2.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.TBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.TBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = TBlock2.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = TBlock2.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = TBlock2.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = TBlock3.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.TBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.TBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = TBlock3.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = TBlock3.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = TBlock3.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = TBlock4.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.TBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.TBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = TBlock4.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = TBlock4.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = TBlock4.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = TBlock1.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.TBlock1;
                            }
                        }
                        break;
                    }
                case PieceType.ZBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = ZBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = ZBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = ZBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = ZBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 1);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.ZBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.ZBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = ZBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = ZBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = ZBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = ZBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 2);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.ZBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.ZBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = ZBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = ZBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = ZBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = ZBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 3);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.ZBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.ZBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = ZBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = ZBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = ZBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = ZBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 4);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.ZBlock1;
                            }
                        }
                        break;
                    }
                case PieceType.SBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = SBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = SBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = SBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = SBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 1);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.SBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.SBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = SBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = SBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = SBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = SBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 2);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.SBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.SBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = SBlockHorizontal.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = SBlockHorizontal.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = SBlockHorizontal.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = SBlockVertical.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 3);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.SBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.SBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = SBlockVertical.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = SBlockVertical.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = SBlockVertical.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = SBlockHorizontal.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1), 4);

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.SBlock1;
                            }
                        }
                        break;
                    }
                case PieceType.LBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = LBlock1.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = LBlock1.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = LBlock1.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = LBlock2.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.LBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.LBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = LBlock2.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = LBlock2.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = LBlock2.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = LBlock3.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.LBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.LBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = LBlock3.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = LBlock3.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = LBlock3.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = LBlock4.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.LBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.LBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = LBlock4.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = LBlock4.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = LBlock4.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = LBlock1.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.LBlock1;
                            }
                        }
                        break;
                    }
                case PieceType.JBlock1:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = JBlock1.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = JBlock1.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = JBlock1.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = JBlock2.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.JBlock2;
                            }
                        }
                        break;
                    }
                case PieceType.JBlock2:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = JBlock2.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = JBlock2.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = JBlock2.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = JBlock3.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.JBlock3;
                            }
                        }
                        break;
                    }
                case PieceType.JBlock3:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = JBlock3.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = JBlock3.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = JBlock3.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = JBlock4.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.JBlock4;
                            }
                        }
                        break;
                    }
                case PieceType.JBlock4:
                    {
                        if (stepReached)
                        {
                            bool placedDown;

                            (placedDown, newPieceValues) = JBlock4.CalculateDownPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (placedDown)
                            {
                                piecePlacedDown = true;
                                return;
                            }
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.left)
                        {
                            newPieceValues = JBlock4.CalculateLeftPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.right)
                        {
                            newPieceValues = JBlock4.CalculateRightPosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));
                        }

                        if (currentMove != null && currentMove.moveType == MoveType.rotate)
                        {
                            bool blocked;

                            (blocked, newPieceValues) = JBlock1.CalculateFromRotatePosition(currentPiece.xPosition, currentPiece.yPosition, array.GetLength(0), array.GetLength(1));

                            if (blocked)
                            {
                                newPieceValues = null;
                            }
                            else
                            {
                                currentPiece.pieceType = PieceType.JBlock1;
                            }
                        }
                        break;
                    }
            }

            if (newPieceValues != null)
            {
                Debug.Log(string.Join(" ", newPieceValues.Cast<int>()));
                if (!CheckBox(newPieceValues))
                {
                    if (currentPiece.values != null) EmptyBox(currentPiece.values);
                    FillBox(newPieceValues);
                    currentPiece.xPosition = newPieceValues[0, 0];
                    currentPiece.yPosition = newPieceValues[0, 1];              
                    currentPiece.values = new int[4, 2];
                    Array.Copy(newPieceValues, currentPiece.values, newPieceValues.Length);
                    if (fixedGamePlay) MoveInteractor();
                }
                else
                {
                    if((newPieceValues[0, 1] == yLength - 1 || newPieceValues[1, 1] == yLength - 1 || newPieceValues[2, 1] == yLength - 1 || newPieceValues[3, 1] == yLength - 1) && currentMove == null)
                    {
                        GameOver();
                    }
                    else if(currentMove != null)
                    {

                    }
                    else
                    {
                        piecePlacedDown = true; 
                    }
                }
            }

            currentMove = null;
        }

        //Initialise the bricks in the wall 
        if (!initialised && useSample)
        {
            time = 0f;

            initialised = true;

            CreateBricksSample();

            loadingText.gameObject.SetActive(false);
            playButton.gameObject.SetActive(true);
            fixedGamePlayButton.gameObject.SetActive(true);
        }

        //Initialise the bricks in the wall 
        if (!initialised) 
        {
            time = 0f;

            //Check if scene sense successfully initialised 
            if (!sceneQuerySuccessful) 
            {
                LoadSceneAsync();
                return;
            }

            //All the wall are not yet initialized 
            if(_walls.Count < _orderedRoomGuids.Keys.Count)
            {
                return;
            }

            initialised = true;

            _walls.Sort(_wallOrderComparer);

            CreateBricks();

            loadingText.gameObject.SetActive(false);
            playButton.gameObject.SetActive(true);
            fixedGamePlayButton.gameObject.SetActive(true);
        }
    }

    private void CreateBricks()
    {
        List<List<GameObject>> cubes = new List<List<GameObject>>();
        foreach (OVRScenePlane wall in _walls)
        {
            cubes.Add(wallCubes[wall]);
        }

        xLength = 0;
        yLength = height;

        for (int i = 0; i < cubes.Count; i++)
        {
            xLength += (cubes[i].Count / yLength);
        }

        array = new GamePiece[xLength, yLength];

        for (int k = 0; k < height; k++)
        {
            int l = 0;
            for (int i = 0; i < cubes.Count; i++)
            {
                int m = 0;
                for (int j = ((cubes[i].Count / height) * k); j < ((cubes[i].Count / height) * (k + 1)); j++)
                {
                    array[l + m, k] = cubes[i][j].GetComponent<GamePiece>();
                    array[l + m, k].GetComponent<GamePiece>().SetXPosition(l + m);
                    array[l + m, k].GetComponent<GamePiece>().SetYPosition(k);
                    array[l + m, k].GetComponent<GamePiece>().SetGamePlay(this);
                    m++;
                }
                l += (cubes[i].Count / height);
            }
        }
    }

    private void CreateBricksFixedGamePlay()
    {
        foreach (OVRScenePlane wall in _walls)
        {
            foreach(GameObject go in wallCubes[wall])
            {
                Destroy(go);
            }
        }

        xLength = 40;
        yLength = 12;

        array = new GamePiece[xLength, yLength];

        for (int j = 0; j < yLength; j++)
        {
            for (int i = 0; i < xLength; i++)
            {
                array[i, j] = fixedGamePlayObject.transform.GetChild(i + (j * xLength)).GetComponent<GamePiece>();
                array[i, j].GetComponent<GamePiece>().SetXPosition(i);
                array[i, j].GetComponent<GamePiece>().SetYPosition(j);
                array[i, j].GetComponent<GamePiece>().SetGamePlay(this);
            }
        }

        Transform mainCameraTransform = Camera.main.transform;

        fixedGamePlayObject.transform.position = new Vector3(mainCameraTransform.position.x, _walls[0].transform.position.y - (_walls[0].Dimensions.y/2), mainCameraTransform.position.z);

        fixedGamePlayObject.transform.forward = _walls[0].transform.forward;

    }

    private void CreateBricksSample()
    {
        xLength = 10;
        yLength = 10;

        array = new GamePiece[xLength, yLength];

        for (int j = 0; j < yLength; j++)
        {
            for (int i = 0; i < xLength; i++)
            {
                array[i, j] = sampleObject.transform.GetChild(i + (j * xLength)).GetComponent<GamePiece>();
                array[i, j].GetComponent<GamePiece>().SetXPosition(i);
                array[i, j].GetComponent<GamePiece>().SetYPosition(j);
                array[i, j].GetComponent<GamePiece>().SetGamePlay(this);
            }
        }
    }

    private void MoveInteractor()
    {
        int xPosition = -1;
        int yPosition = -1;

        (xPosition, yPosition) = GetCentrePosition();

        SetInteractorPosition(xPosition, yPosition);
    }

    private (int, int) GetCentrePosition()
    {
        int xPosition = -1;
        int yPosition = -1;

        switch (currentPiece.pieceType)
        {
            case PieceType.IBlock1:
            case PieceType.IBlock2:
            case PieceType.IBlock3:
            case PieceType.IBlock4:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.OBlock1:
            case PieceType.OBlock2:
            case PieceType.OBlock3:
            case PieceType.OBlock4:
                xPosition = currentPiece.values[0, 0];
                yPosition = currentPiece.values[0, 1];
                break;
            case PieceType.TBlock1:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.TBlock2:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.TBlock3:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.TBlock4:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.ZBlock1:
                xPosition = currentPiece.values[0, 0];
                yPosition = currentPiece.values[0, 1];
                break;
            case PieceType.ZBlock2:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.ZBlock3:
                xPosition = currentPiece.values[3, 0];
                yPosition = currentPiece.values[3, 1];
                break;
            case PieceType.ZBlock4:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.SBlock1:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.SBlock2:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.SBlock3:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.SBlock4:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.LBlock1:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.LBlock2:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.LBlock3:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.LBlock4:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.JBlock1:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.JBlock2:
                xPosition = currentPiece.values[1, 0];
                yPosition = currentPiece.values[1, 1];
                break;
            case PieceType.JBlock3:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;
            case PieceType.JBlock4:
                xPosition = currentPiece.values[2, 0];
                yPosition = currentPiece.values[2, 1];
                break;

        }

        return (xPosition, yPosition); 
    }

    private void GameOver()
    {
        interactor.gameObject.SetActive(false);
        OnPauseButtonPressed();
        loadingTextText.text = "Game Over - Score " + score.ToString();
        pauseButton.SetActive(false); 
        resetButton.SetActive(true);
    }

    private void EndGame()
    {
        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                EmptyBox(i, j);
            }
        }

        currentPiece = null;
        currentMove = null;
        score = 0;
        level = 0;
        lines = 0; 
    }

    public bool CheckBox(int[,] values)
    {
        for (int i = 0; i < values.GetLength(0); i++)
        {
                if (values[i, 0] != -1 && values[i, 1] != -1 && CheckBox(values[i, 0], values[i, 1])) return true; 
        }

        return false;
    }

    private bool CheckBox(int x, int y)
    {
        return array[x, y].CheckBox();
    }

    public void FillBox(int[,] values, bool lockIt = false)
    {
        for (int i = 0; i < values.GetLength(0); i++)
        {
            if (values[i, 0] != -1 && values[i, 1] != -1)
                FillBox(values[i, 0], values[i, 1], lockIt);
        }
    }

    private void FillBox(int x, int y, bool lockIt, bool dontChangeColour = false)
    {
        array[x, y].FillBox(currentPiece.color, lockIt, dontChangeColour);
    }

    public void EmptyBox(int[,] values)
    {
        for (int i = 0; i < values.GetLength(0); i++)
        {
            if (values[i, 0] != -1 && values[i, 1] != -1)
                EmptyBox(values[i, 0], values[i, 1]);
        }

    }

    private void EmptyBox(int x, int y)
    {
        array[x, y].EmptyBox(); 
    }

    private void SetInteractorPosition(int x, int y)
    {
        if(x == -1 || y == -1)
        {
            x = currentPiece.values[0, 0];
            y = currentPiece.values[1, 0];
        }

        array[x, y].SetInteractorPosition(interactor);
    }

    [ContextMenu("OnPlayButtonPressed")]
    public void OnPlayButtonPressed()
    {
        OnPlayButtonPressed(true);
    }

    public void OnPlayButtonPressed(bool dynamicGamePlay)
    {
        fixedGamePlay = dynamicGamePlay ? false : true;

        if (fixedGamePlay)
        {
            CreateBricksFixedGamePlay();
        }

        for (int j = 0; j < yLength; j++)
        {
            for (int i = 0; i < xLength; i++)
            {
                array[i, j].gameObject.SetActive(true);
            }
        }

        interactor = fixedGamePlay ? interactorFollowPiece : interactorFollowCamera; 

        EndGame(); 
        playButton.SetActive(false);
        fixedGamePlayButton.SetActive(false);
        pauseButton.SetActive(true);
        resetButton.SetActive(false);
        endButton.SetActive(true);
        gameStarted = true;
        gamePaused = false;
        pauseResumeText.text = gamePaused ? "Resume" : "Pause";
        loadingText.SetActive(true);
        loadingTextText.text = "Score " + score.ToString(); 

        interactor.gameObject.SetActive(true);
    }


    [ContextMenu("OnPauseButtonPressed")]
    public void OnPauseButtonPressed()
    {
        gamePaused = !gamePaused;

        pauseResumeText.text = gamePaused ? "Resume" : "Pause"; 
    }


    [ContextMenu("OnResetButtonPressed")]
    public void OnResetButtonPressed()
    {
        OnPlayButtonPressed(true);
    }


    [ContextMenu("OnEndButtonPressed")]
    public void OnEndButtonPressed()
    {
        playButton.SetActive(true);
        fixedGamePlayButton.SetActive(true);
        pauseButton.SetActive(false);
        resetButton.SetActive(false);
        endButton.SetActive(false);
        endPressed = true;
        loadingText.SetActive(false);

        interactor.gameObject.SetActive(false);
    }

    public void MovePlayed(string move)
    {
        MoveType moveType = MoveType.left;

        switch (move)
        {
            case "left":
                moveType = MoveType.left;
                break;
            case "right":
                moveType = MoveType.right;
                break;
            case "down":
                moveType = MoveType.down;
                break;
            case "rotate":
                moveType = MoveType.rotate;
                break;
        }
        currentMove = new Move();
        currentMove.moveType = moveType;
    }

    public void AddCubes(List<GameObject> cubesToAdd, int y, OVRScenePlane plane)
    {
        height = y;
        _walls.Add(plane);
        wallCubes[plane] = cubesToAdd;
    }

    async void LoadSceneAsync()
    {
        // fetch all rooms, with a SceneCapture fallback
        var rooms = new List<OVRAnchor>();
        await OVRAnchor.FetchAnchorsAsync<OVRRoomLayout>(rooms);
        if (rooms.Count == 0)
        {
            var sceneCaptured = await SceneManagerHelper.RequestSceneCapture();
            if (!sceneCaptured)
                return;

            await OVRAnchor.FetchAnchorsAsync<OVRRoomLayout>(rooms);
        }

        // fetch room elements, create objects for them
        var tasks = rooms.Select(async room =>
        {
            var roomObject = new GameObject($"Room-{room.Uuid}");
            if (!room.TryGetComponent(out OVRAnchorContainer container))
                return;
            if (!room.TryGetComponent(out OVRRoomLayout roomLayout))
                return;

            var children = new List<OVRAnchor>();
            await container.FetchChildrenAsync(children);

            if (!roomLayout.TryGetRoomLayout(out var ceilingUuid, out var floorUuid, out var wallUuids))
            {
                return;
            }

            _orderedRoomGuids.Clear();
            int validWallsCount = 0;
            foreach (var wallUuid in wallUuids)
            {
                sceneQuerySuccessful = true;
                _orderedRoomGuids[wallUuid] = validWallsCount++;
                if (!wallUuid.Equals(Guid.Empty)) _uuidToQuery.Add(wallUuid);
            }

        }).ToList();
        await Task.WhenAll(tasks);
    }

    enum PieceType
    {
        IBlock1,
        IBlock2,
        IBlock3,
        IBlock4,
        OBlock1,
        OBlock2,
        OBlock3,
        OBlock4,
        TBlock1,
        TBlock2,
        TBlock3,
        TBlock4,
        ZBlock1,
        ZBlock2,
        ZBlock3,
        ZBlock4,
        SBlock1,
        SBlock2,
        SBlock3,
        SBlock4,
        LBlock1,
        LBlock2,
        LBlock3,
        LBlock4,
        JBlock1,
        JBlock2,
        JBlock3,
        JBlock4
    }

    class Piece
    {
        public PieceType pieceType;
        public int xPosition = -1;
        public int yPosition = -1;
        public int[,] values;
        public Color color;
    }

    public enum MoveType
    {
        left,
        right,
        down,
        rotate
    }

    class Move
    {
        public MoveType moveType;
    }

    private Color[] colors = new Color[3] { Color.red, Color.yellow, Color.blue };
}
