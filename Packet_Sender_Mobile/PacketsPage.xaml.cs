﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Collections.ObjectModel;


using Sockets;
using Sockets.Plugin; //nuget https://github.com/rdavisau/sockets-for-pcl

using System.Diagnostics;
using SQLite;

namespace Packet_Sender_Mobile
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PacketsPage : ContentPage
    {

        private ObservableCollection<Packet> _thepackets;
        private TcpSocketClient tcp;
        private UdpSocketClient udp;
        private TcpSocketListener tcpServer;
        private UdpSocketReceiver udpServer;

        public int tcpPort;
        public int udpPort;

        public Packet thepacket;

		private SQLiteAsyncConnection _connection;

		bool _isDataLoaded;


		public PacketsPage()
        {
            InitializeComponent();

            tcpPort = SettingsPage.TCPPort;
            udpPort = SettingsPage.UDPPort;

            tcpServer = new TcpSocketListener();
            udpServer = new UdpSocketReceiver();

            tcpServer.ConnectionReceived += TcpConnection;
            udpServer.MessageReceived += UdpConnection;

            Task.Run(async () =>
            {
                await tcpServer.StartListeningAsync(tcpPort);
                await udpServer.StartListeningAsync(udpPort);
                SettingsPage.TCPPort = tcpServer.LocalPort;

                MessagingCenter.Send(this, Events.BOUND_PORTS_CHANGED, 0);
            });

            
            //udpServer.StartListeningAsync(udpPort);

            /*
            += async (sender, args) =>
        {
            var client = args.SocketClient;

            var bytesRead = -1;
            var buf = new byte[1];

            while (bytesRead != 0)
            {
                bytesRead = await args.SocketClient.ReadStream.ReadAsync(buf, 0, 1);
                //if (bytesRead > 0)
                //    Debug.Write(buf[0]);
            }
        };
        */

            _connection = DependencyService.Get<ISQLiteDb>().GetConnection();

            /*
			var demopackets = Packet.GetDemoPackets();
            for (int i = 0; i < demopackets.Count(); i++) {
                _thepackets.Add(demopackets[i]);
            }
			packetListView.ItemsSource = _thepackets;
			*/


			tcp = new TcpSocketClient();
            udp = new UdpSocketClient();  
            thepacket = new Packet();

            MessagingCenter.Subscribe<LoginPage, List<Packet>>(this, Events.NEW_PACKET_LIST, OnNewPacketListAsync);
            MessagingCenter.Subscribe<ImportCloud, List<Packet>>(this, Events.NEW_PACKET_LIST, OnNewPacketListAsyncIC);
            MessagingCenter.Subscribe<PacketEditPage, Packet>(this, Events.PACKET_MODIFIED, OnPacketModified);

        }



        void UdpConnection(object sender, Sockets.Plugin.Abstractions.UdpSocketMessageReceivedEventArgs args)
        {


            Packet pkt = new Packet();
            pkt.method = "udp";
            pkt.fromip = args.RemoteAddress;
            pkt.fromport = Convert.ToInt32(args.RemotePort);
            pkt.toip = "You";
            pkt.toport = udpPort;

            var buf = args.ByteData;

            if (buf.Length > 0)
            {
                pkt.hex = Packet.byteArrayToHex(buf);
            }
            

            MessagingCenter.Send(this, Events.NEW_TRAFFIC_PACKET, pkt);
        }




        async void TcpConnection(object sender, Sockets.Plugin.Abstractions.TcpSocketListenerConnectEventArgs args)
        {

            var client = args.SocketClient;

            Packet pkt = new Packet();
            pkt.method = "tcp";
            pkt.fromip = client.RemoteAddress;
            pkt.fromport = client.RemotePort;
            pkt.toip = "You";
            pkt.toport = tcpServer.LocalPort;


            var bytesRead = -1;
            var buf = new byte[1024];
            bytesRead = await args.SocketClient.ReadStream.ReadAsync(buf, 0, 1024);

            if (bytesRead > 0) {
                byte[] saveArray = new byte[bytesRead];
                Array.Copy(buf, saveArray, saveArray.Length);
                pkt.hex = Packet.byteArrayToHex(saveArray);
            }


            //TODO:make persistent?
            await client.DisconnectAsync();
            
            MessagingCenter.Send(this, Events.NEW_TRAFFIC_PACKET, pkt);
        }



        protected override async void OnAppearing()
		{
			// In a multi-page app, everytime we come back to this page, OnAppearing
			// method is called, but we want to load the data only the first time
			// this page is loaded. In other words, when we go to ContactDetailPage
			// and come back, we don't want to reload the data. The data is already
			// there. We can control this using a switch: isDataLoaded.
			if (_isDataLoaded)
				return;

			_isDataLoaded = true;

			// I've extracted the logic for loading data into LoadData method. 
			// Now the code in OnAppearing method looks a lot cleaner. The 
			// purpose is very explicit. If data is loaded, return, otherwise,
			// load data. Details of loading the data is delegated to LoadData
			// method. 
			await LoadData();

			base.OnAppearing();
		}

		private async Task LoadData()
		{
			await _connection.CreateTableAsync<Packet>();

			var pkts = await _connection.Table<Packet>().ToListAsync();
            List<Packet> SortedList = pkts.OrderBy(o => o.name).ToList();
            _thepackets = new ObservableCollection<Packet>(SortedList);
			packetListView.ItemsSource = _thepackets;
		}



        private void OnPacketModified(PacketEditPage source, Packet packet)
        {
            Debug.WriteLine("Updating main list with " + packet.name + " headed to " + packet.toip);

            bool found = false;
            for (int i = 0; i < _thepackets.Count(); i++)
            {
                if (_thepackets[i].name == packet.name) {
                    Packet freshpacket = new Packet(); //forces view refresh. 
                    freshpacket.Clone(packet);
                    _thepackets[i] = freshpacket;
                    found = true;
                    break;
                }
                
            }

            if (!found) {
                _thepackets.Add(packet);
            }
            

            //packetListView.ItemsSource = newList;



        }

        private void OnNewPacketListAsyncIC(ImportCloud source, List<Packet> newList)
        {
            OnNewPacketListAsync(null, newList);
        }


            private void OnNewPacketListAsync(LoginPage source, List<Packet> newList)
        {
            Debug.WriteLine("List now has " + newList.Count());
            Debug.WriteLine("Updating main list");


            for (int i = 0; i < _thepackets.Count(); i++)
            {
                _connection.DeleteAsync(_thepackets[i]);

			}

			_thepackets.Clear();

            for (int i = 0; i < newList.Count(); i++)
            {
				_connection.InsertAsync(newList[i]);
				_thepackets.Add(newList[i]);
            }

            //packetListView.ItemsSource = newList;



        }
        private void packetListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            Debug.WriteLine("Selected");

            thepacket = packetListView.SelectedItem as Packet;

            if (thepacket.unitTests())
            {
                //DisplayAlert("Alert", "Unit tests passed!", "OK");
            }
            else
            {
                DisplayAlert("Alert", "Unit tests failed", "OK");
            }


        }

        private void sendButton_Clicked(object sender, EventArgs e)
        {
            var sendpacket = packetListView.SelectedItem as Packet;
            if (sendpacket == null)
            {
                Debug.WriteLine("sendButton_Clicked with null");

            } else {

                Task.Run(() =>
                {
                    doSend(sendpacket);
                });
            }

        }

        public async void doSend(Packet sendpacket)
        {
            Debug.WriteLine($"doSend {sendpacket.method} {sendpacket.toip} {sendpacket.toport} {sendpacket.ascii}");
            byte[] bytesToSend = System.Text.Encoding.UTF8.GetBytes(sendpacket.ascii);

            try
            {
                if (sendpacket.isTCP())
                {
                    await tcp.ConnectAsync(sendpacket.toip, sendpacket.toport);
                    await tcp.WriteStream.WriteAsync(bytesToSend, 0, bytesToSend.Length);
                    await tcp.DisconnectAsync();
                }
                if (sendpacket.isUDP())
                {
                    await udpServer.SendToAsync(bytesToSend, sendpacket.toip, sendpacket.toport);
                }

            }
            catch (Exception eSend)
            {
                sendpacket.error = "Error: "+eSend.Message;
                Debug.WriteLine("Exception : " + eSend.Message);
            }




            MessagingCenter.Send(this, Events.NEW_TRAFFIC_PACKET, sendpacket);


        }

        private void deleteButton_Clicked(object sender, EventArgs e)
        {
            Debug.WriteLine("Need to delete " + thepacket.ascii);

			if(_thepackets.IndexOf(thepacket) > -1) {
				Debug.WriteLine("Deleted packet");
				_thepackets.Remove(thepacket);
                _connection.DeleteAsync(thepacket);
            } else {
				Debug.WriteLine("Did not delete packet");
			}




		}

        private async void modifyButton_Clicked(object sender, EventArgs e)
        {
			var sendpacket = packetListView.SelectedItem as Packet;
			if (sendpacket == null)
			{
				Debug.WriteLine("modifyButton_Clicked with null");
                return;

			}

			Debug.WriteLine("modifyButton_Clicked " + sendpacket.name +" " + sendpacket.method);
            //Navigation.PushAsync((new PacketEditPage()));
            await Navigation.PushModalAsync(new PacketEditPage(sendpacket));

        }

        private async void newButton_Clicked(object sender, EventArgs e)
        {
            thepacket = new Packet();
            Debug.WriteLine("newButton_Clicked");
            await Navigation.PushModalAsync(new PacketEditPage(thepacket));
        }
    }


}