using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Cube;
using Random = UnityEngine.Random;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp;

    public GameObject Cube;

    // Start is called before the first frame update
    void Start()
    {
        // TODO: Create Cube
        Cube = Resources.Load("Cube", typeof(GameObject)) as GameObject;

        udp = new UdpClient();
        
        udp.Connect("54.82.245.79", 12345);

        Debug.Log("54.82.245.79");

        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
      
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1);


    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        SPAWN,
        DELETE
    };


    
    [Serializable]
    public class Message{
        public commands cmd;
        public Player[] players;
    }

    [Serializable]
    public class Player{
        public string id;
        public struct receivedColor{
            public float R;
            public float G;
            public float B;
        }
        public receivedColor color;    
        
        public struct receivedPosition
        {
            public float X;
            public float Y;
            public float Z;
        }
        public receivedPosition position;
    }

    [Serializable]
    public class NewPlayer{
        
    }

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    public Message latestMessage;
    public GameState lastestGameState;

    public Queue<Message> spawnMessages = new Queue<Message>();
    public Queue<Message> updateMessages = new Queue<Message>();
    public Queue<Message> deleteMessages = new Queue<Message>();
    public Dictionary<string, GameObject> networkedPlayers = new Dictionary<string, GameObject>();

    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        Debug.Log("Got this: " + returnData);
        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    break;
                case commands.UPDATE:
                    lastestGameState = JsonUtility.FromJson<GameState>(returnData);
                    Debug.Log("Update player");
                    break;
                case commands.DELETE:
                    deleteMessages.Enqueue(latestMessage);
                    Debug.Log("Delete player");
                    break;
                case commands.SPAWN:
                    spawnMessages.Enqueue(latestMessage);
                    Debug.Log("Spawn player");
                    break;
                default:
                    Debug.Log("Error");
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }
    //------------------------------------------
    //-----------------------------
    //-----------------------------


    void SpawnPlayers(){
        while (spawnMessages.Count > 0)
        {
            var spawnMessage = spawnMessages.Dequeue();
            for (int i = 0; i < spawnMessage.players.Length; i++)
            {
                GameObject newCube = Instantiate(Cube, new Vector3(Random.Range(-2f, 2f), 2f, Random.Range(-2f, 2f)), Quaternion.Euler(0, 0, 0)) as GameObject;
                newCube.GetComponent<PlayerCube>().ChangePosition(spawnMessage.players[i].position.X, spawnMessage.players[i].position.Y, spawnMessage.players[i].position.Z);
                Debug.Log("Change Color");
                newCube.GetComponent<PlayerCube>().ChangeColor(spawnMessage.players[i].color.R, spawnMessage.players[i].color.G, spawnMessage.players[i].color.B);
                networkedPlayers.Add(spawnMessage.players[i].id, newCube);
            }
        }
    }

    void UpdatePlayers(){
        while (updateMessages.Count > 0)
        {
            var updateMessage = updateMessages.Dequeue();
            for (int i = 0; i < updateMessage.players.Length; i++)
            {
                var cubeId = updateMessage.players[i].id;
                if (networkedPlayers.ContainsKey(cubeId))
                {
                    networkedPlayers[cubeId].GetComponent<PlayerCube>().ChangePosition(updateMessage.players[i].position.X, updateMessage.players[i].position.Y, updateMessage.players[i].position.Z);
                    networkedPlayers[cubeId].GetComponent<PlayerCube>().ChangeColor(updateMessage.players[i].color.R, updateMessage.players[i].color.G, updateMessage.players[i].color.B);
                }
            }
        }
    }

    void DestroyPlayers(){
        while (deleteMessages.Count > 0)
        {
            var deleteMessage = deleteMessages.Dequeue();
            for (int i = 0; i < deleteMessage.players.Length; i++)
            {
                var cubeId = deleteMessage.players[i].id;
                if (networkedPlayers.ContainsKey(cubeId))
                {
                    Destroy(networkedPlayers[cubeId]);
                    networkedPlayers.Remove(cubeId);
                }
            }
        }
    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}