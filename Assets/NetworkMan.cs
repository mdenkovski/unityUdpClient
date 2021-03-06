﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp;
    // Start is called before the first frame update
    void Start()
    {
        udp = new UdpClient();
        
        udp.Connect("ec2-18-220-46-6.us-east-2.compute.amazonaws.com", 12345);
        //udp.Connect("localhost", 12345);

        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
      
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1);

        InvokeRepeating("UpdatePosition", 0.03333f, 0.03333f);
    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        DROPPED
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
        public Player player;
    }
    
    [Serializable]
    public class Player{
        [Serializable]
        public struct receivedColor{
            public float R;
            public float G;
            public float B;
        }
        public string id;
        public receivedColor color;
        public float posX = 0;
        public float posY = 0;
        public float posZ = 0;
        public bool spawned = true;
        public GameObject playerCube;

    }

    [Serializable]
    public class NewPlayer{
        
    }

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    public Message latestMessage;
    public GameState latestGameState;
    public List<Player> connectedPlayers;
    public string myId = "";
    public GameObject multiplayerCube;
    public List<Player> droppedPlayers;


   
    

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
                    print("new client connected");

                    
                    if(myId == "")
                    {
                        myId = latestMessage.player.id;
                        Debug.Log("My id is:" + myId);
                    }

                    latestGameState = JsonUtility.FromJson<GameState>(returnData);

                    //for every player in the current game state is there a player that we dont already have in our player list
                    for (int i = 0; i < latestGameState.players.Length; i++)
                    {
                        bool playerFound = false;
                        foreach (Player player in connectedPlayers)
                        {
                            if(latestGameState.players[i].id == player.id)
                            {
                                playerFound = true;
                                
                            }

                        }
                        if(!playerFound) // the player in the latest game state is new and we need to add them
                        {
                            connectedPlayers.Add(latestGameState.players[i]);
                        }

                    }
                    break;
                case commands.UPDATE:
                    latestGameState = JsonUtility.FromJson<GameState>(returnData);
                    for (int i = 0; i < latestGameState.players.Length; i++)
                    {
                        foreach(Player player in connectedPlayers)
                        {
                            if(player.id == latestGameState.players[i].id)
                            {
                                //update current player list colours with the colours from the server
                                //player.color.R = latestGameState.players[i].color.R;
                                // player.color.G = latestGameState.players[i].color.G;
                                //player.color.B = latestGameState.players[i].color.B;

                                //update current player list positoins
                                player.posX = latestGameState.players[i].posX;
                                player.posY = latestGameState.players[i].posY;
                                player.posZ = latestGameState.players[i].posZ;
                            }
                        }

                    }
                    break;
                case commands.DROPPED:
                    foreach (Player player in connectedPlayers)
                    {
                        if (player.id == latestMessage.player.id)
                        {
                            Debug.Log("found dropped player");
                            droppedPlayers.Add(player);
                        }
                    }
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

    void SpawnPlayers(){


        foreach (Player player in connectedPlayers)
        {
            if (player.spawned == true)
            {
                GameObject cube = GameObject.Instantiate(multiplayerCube, new Vector3(player.posX, player.posY, player.posZ), Quaternion.identity);
                cube.GetComponent<CubeScript>().id = player.id;
                player.playerCube = cube;
                player.spawned = false;

            }
        }
        


    }

    void UpdatePlayers(){

        //update player position
        foreach (Player player in connectedPlayers)
        {
            //not changing colour anymore
            //Renderer cubeRender = player.playerCube.GetComponent<Renderer>();
            //cubeRender.material.color = new Color(player.color.R, player.color.G, player.color.B);

            //update all the other players positions
            if (player.id != myId)
            {
                player.playerCube.transform.position = new Vector3(player.posX, player.posY, player.posZ);
            }
           
        }
    }

    void DestroyPlayers(){

        //remove the dropped player from our conencted player list
        foreach (Player droppedPlayer in droppedPlayers)
        {
            connectedPlayers.Remove(droppedPlayer);
        }

        //destroy the cube of the player
       foreach (Player droppedPlayer in droppedPlayers)
       {
            Destroy(droppedPlayer.playerCube);
       }
       //clear the dropped players
       droppedPlayers.Clear();

    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void UpdatePosition()
    {
        foreach (Player player in connectedPlayers)
        {
            if (player.id == myId) //find our player
            {

                player.posX = player.playerCube.transform.position.x;
                player.posY = player.playerCube.transform.position.y;
                player.posZ = player.playerCube.transform.position.z;


                string playerJSON = JsonUtility.ToJson(player);
                string message = "update" + playerJSON;
                //Debug.Log(message);
                Byte[] sendBytes = Encoding.ASCII.GetBytes(message);
                udp.Send(sendBytes, sendBytes.Length);
            }
        }
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }

    void OnApplicationQuit()
    {
        OnDestroy();
    }
}
