open System.Net
open System
open System.IO
#if INTERACTIVE
#r "../packages/FSharp.Data.3.1.1/lib/net45/FSharp.Data.dll"
#endif
open FSharp.Data
open System.Collections.Generic
open NUnit.Framework
open FsUnit

type SampleUrl = JsonProvider<"https://s3-sa-east-1.amazonaws.com/pontotel-docs/data.json">
type Funcionario = JsonProvider<""" {"id":"string", "name":"string", "pwd":"string", "timelogs":[]}""">
type Registro = JsonProvider<""" {"kind":"tipoDeEntrada", "time":"timeRegistro"} """>
type Data = JsonProvider<""" {"pwd":{}} """>

let checkRegisterType registerNum =
    match registerNum with
    | "0" -> "Entrada" 
    | "1" -> "Pausa"
    | "2" -> "Fim de pausa"
    | "3" -> "Saída"
    | _ -> "Error"

let formatTime (timeinput:string) =
    let substringList = [for i in 0 .. 2 .. (timeinput.Length - 2) -> timeinput.Substring(i,2)]
    let date = String.Format("{0}{1}-{2}-{3}", substringList.Item(0), substringList.Item(1), substringList.Item(2), substringList.Item(3))
    let time = String.Format("{0}:{1}:{2}", substringList.Item(4),substringList.Item(5),substringList.Item(6))
    date + "T" + time

let createTimelog (registerInput:string) =
    let typeRegister = checkRegisterType (registerInput.Substring(4,1))
    let time = formatTime (registerInput.Substring(5))
    let jsonData = String.Format("\"kind\":\"{0}\", \"time\":\"{1}\"", typeRegister, time)
    let completeString = """{""" + jsonData + """}"""
    Registro.Parse(completeString)

let formatJsonFuncionario (funcionario , listaRegistros:Registro.Root list) =
    let funcioarioSample = Funcionario.Parse(funcionario)
    let data = String.Format("\"name\":\"{0}\", \"id\":\"{1}\", \"pwd\":\"{2}\", \"timelogs\":{3}", funcioarioSample.Name, funcioarioSample.Id, funcioarioSample.Pwd, listaRegistros.ToString().Replace(";", ","))
    Funcionario.Parse("""{""" + data + """}""")

let formatPwdWithFuncionario (pwd:string, funcionario:Funcionario.Root) =
    String.Format("\"{0}\":{1}", pwd, funcionario)

[<EntryPoint>]
let main argv = 
    // Criação das variaveis que serão utilizadas para acessar informações dos funcionários
    // Cria uma variavel que recebe o sample do json fornecido pelo primeiro link
    let JsonResponse = SampleUrl.GetSample()
    // lista comprimida transformando os index do sample url em dicionarios para acessar as informações dos funcionarios
    let funcionarios = List.init JsonResponse.Data.Length (fun index -> Funcionario.Parse(JsonResponse.Data.GetValue(index).ToString()))

    // Criação das variaveis que serão utilizadas para acessar os registros dos pontos dos funcionários
    // referencia do url para request dos teste_pontos
    let url = "https://s3-sa-east-1.amazonaws.com/pontotel-docs/teste_pontos.txt"
    // request da url
    let requestUrl = Http.RequestString(url)
    // lista dos registros, separando o request por linhas
    let registros = Seq.toList (requestUrl.Split [|'\n'|])
    // lista com todos os pwd dos registros de pontos, ordenados do maior para o menor e retirando os pwd duplicados
    let pwdRegistros = List.rev ( List.ofSeq (set (List.init registros.Length (fun index -> registros.Item(index).Substring(0,4)))))
    // criação de um Dicionario para associar os pwd com os seus respectivos registros
    let dictRegistro = new Dictionary<string, string list>()
    // for que percorre do valor 0 até o tamanho da lista de possiveis pwds - 1 
    for i = 0 to (pwdRegistros.Length - 1) do
        // Adiciona ao dicionario o valor do pwd como Key e atribui ao Value uma lista feita a partir de um filtro que retorna quais registros equivalem a Key
        dictRegistro.Add (pwdRegistros.Item(i), registros |>  List.filter (fun index -> index.Substring(0,4).Equals(pwdRegistros.Item(i))))

    // criação da lista que guardará os valores de associação dos PWD com o respectivo funcionário e seu timelog
    let dataPwdFuncionario = new List<string>()
    // for que percorre todos os itens do dicionario que contem os registros separados por PWD
    for item in dictRegistro do
        // variavel para guardar as informações do funcionario em relação ao pwd lido no momento
        // é realizado um filtro em relação ao item lido da lista funcionarios que o valor é igual ao pwd atual
        let funcionario = funcionarios |> List.filter (fun x -> x.Pwd.ToString().Equals(item.Key))
        // criação da lista que guarda os Registros/timelogs em json referente ao pwd atual e a quantidade de registros feitos para este mesmo pwd 
        let listaRegistros = List.init item.Value.Length (fun index -> createTimelog (item.Value.Item(index)))
        // criação do Json referente a associação do funcionário com o seu timelog
        let jsonFuncionario = formatJsonFuncionario (funcionario.Head.ToString(), listaRegistros)
        // formatação do pwd atual com o json referente ao funcionario já com timelog
        let pwdWithFuncionario = formatPwdWithFuncionario (item.Key, jsonFuncionario)
        // Adição da string final a lista de associação de PWD com Funcionário
        dataPwdFuncionario.Add(pwdWithFuncionario)

    let PWDFuncionarioJson = Data.Parse("{" + String.concat "," dataPwdFuncionario + "}")
    printfn "%s" (PWDFuncionarioJson.JsonValue.ToString())
    System.Console.ReadKey() |> ignore
    0