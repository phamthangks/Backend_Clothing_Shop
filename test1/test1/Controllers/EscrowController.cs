using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;

namespace test1.Controllers
{
	[ApiController]
	[Route("api/escrow")]
	public class EscrowController : ControllerBase
	{
		private readonly Web3 _web3;
		// Thay bằng private key thật của bạn (cẩn trọng với bảo mật)
		private readonly string _privateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
		private readonly string _sellerAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";   // Địa chỉ người bán
		private readonly string _arbiterAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC"; // Địa chỉ bên trung gian
		private readonly string _abi;
		private readonly string _bytecode;

		public EscrowController()
		{
			// Cập nhật chainId theo Hardhat node mặc định là 31337
			var account = new Account(_privateKey, chainId: 31337);
			// Cập nhật URL RPC của Hardhat node (mặc định: http://127.0.0.1:8545)
			_web3 = new Web3(account, "http://127.0.0.1:8545");
			Console.WriteLine("Account address: " + _web3.TransactionManager.Account.Address);

			// Đảm bảo rằng file ABI và Bytecode (build từ Hardhat) được đặt đúng vị trí
			_abi = System.IO.File.ReadAllText("SmartContracts/EscrowABI.json");
			_bytecode = System.IO.File.ReadAllText("SmartContracts/EscrowBytecode.txt");
		}

		// API deploy hợp đồng thông minh
		[HttpPost("deploy")]
		public async Task<IActionResult> DeployContract()
		{
			try
			{
				// Ví dụ gửi kèm 1 ETH vào hợp đồng
				var value = new HexBigInteger(Web3.Convert.ToWei(1));

				var transactionReceipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
					_abi, _bytecode,
					_web3.TransactionManager.Account.Address,
					new HexBigInteger(3000000),  // Gas limit
												  value,                    // Nếu muốn gửi kèm ETH, bỏ comment dòng này
					System.Threading.CancellationToken.None,
					_sellerAddress,             // Tham số constructor: địa chỉ người bán
					_arbiterAddress);           // Tham số constructor: địa chỉ bên trung gian

				return Ok(new { ContractAddress = transactionReceipt.ContractAddress });
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return BadRequest(ex.Message);
			}
		}

		// API lấy số dư của hợp đồng
		[HttpGet("balance/{contractAddress}")]
		public async Task<IActionResult> GetContractBalance(string contractAddress)
		{
			try
			{
				var contract = _web3.Eth.GetContract(_abi, contractAddress);
				var getBalanceFunction = contract.GetFunction("getBalance");
				var balance = await getBalanceFunction.CallAsync<BigInteger>();
				return Ok(new { Balance = Web3.Convert.FromWei(balance) });
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}
	}
}
