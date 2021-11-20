using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CadastroPessoas.Models.Dao
{
    public class PessoaDao
    {

         string connectionString = @"Data Source=localhost,11433;Initial Catalog=pim_viii;User Id=sa;Password=Pedrinho16*;"; 

         private SqlConnection connection;

         private SqlConnection getConnection() 
         {
             if (connection == null) 
             {
                 connection = new SqlConnection(connectionString);
             } 
             else if (connection.State == ConnectionState.Closed) 
             {
                 connection.Dispose();
                 connection = new SqlConnection(connectionString);
             }
             connection.Open();
             return connection;
         }

         private SqlCommand createSqlCommand(string sql, Dictionary<string, object> parameters) 
         {
             var cmd = getConnection().CreateCommand();
             cmd.CommandText = sql;
             
             if (parameters != null)
                foreach (string param in parameters.Keys)
                    cmd.Parameters.AddWithValue(param, parameters[param]);

            return cmd;
         }

         private int executeUpdateSql(string sql, Dictionary<string, object> parameters)
         {
             using (var cmd = createSqlCommand(sql, parameters))
             {
                 return cmd.ExecuteNonQuery();
             };
         }

         private int executeInsertSql(string sql, Dictionary<string, object> parameters)
         {
             sql += ";Select SCOPE_IDENTITY();";
             using (var cmd = createSqlCommand(sql, parameters))
             {
                 return (int)cmd.ExecuteScalar();
             };
         }

        private IList<Dictionary<string, object>> executeSql(string sql) 
        {
            return executeSql(sql, null);
        }

         private IList<Dictionary<string, object>> executeSql(string sql, Dictionary<string, object> parameters)
         {
             using (var cmd = createSqlCommand(sql, parameters))
             {

                using (SqlDataReader reader = cmd.ExecuteReader()) 
                {
                    IList<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
                    while(reader.Read()) {
                        Dictionary<string, object> linha = new Dictionary<string, object>();
                        for(int i = 0; i < reader.FieldCount; i++){
                            linha.Add(reader.GetName(i).ToUpper(), reader.GetValue(i));
                        } ;
                        list.Add(linha);
                    }

                    return list;
                }

             };
         }

        public bool exclua(Pessoa p)
        {
            using (var trans = connection.BeginTransaction()) 
            {
                 try
                {
                    deletarPessoaTelefone(p);
                    executeUpdateSql(@"Delete FROM PESSOA Where ID_PESSOA = " + p.Id, null);
                    
                    // Remover endereços órfãos
                    executeUpdateSql(@"Delete FROM ENDERECO e Where not exists (Select 1 From PESSOA p Where p.ID_ENDERECO = e.ID)", null);

                    // Remover telefone órfãos
                    executeUpdateSql(@"Delete FROM TELEFONE t Where not exists (Select 1 From PESSOA_TELEFONE pt Where pt.ID_TELEFONE = t.ID)", null);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
        }

        public bool insira(Pessoa p)
        {
            using (var trans = connection.BeginTransaction()) 
            {
                try
                {
                    salvarOuAtualizarEndereco(p.Endereco);
                    salvarOuAtualizarPessoa(p);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    Console.WriteLine(ex.ToString());
                    return false;
                    
                }
            }
        }

        public bool altere(Pessoa p)
        {
            using (var trans = connection.BeginTransaction()) 
            {
                try
                {
                    salvarOuAtualizarEndereco(p.Endereco);
                    salvarOuAtualizarPessoa(p);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    Console.WriteLine(ex.ToString());
                    return false;
                    
                }
            }
        }

        private void salvarOuAtualizarEndereco(Endereco endereco) 
        {
            string sql = endereco.Id != 0 ? 
                    "Update ENDERECO Set LOGRADOURO = @LOGRADOURO, NUMERO = @NUMERO, CEP = @CEP, BAIRRO = @BAIRRO,  " +
                    "CIDADE = @CIDADE, ESTADO = @ESTADO" +
                    "Where ID = @ID" 
                    :
                    "Insert Into ENDERECO(LOGRADOURO, NUMERO, CEP, BAIRRO, CIDADE, ESTADO) " +
                            "Values (@LOGRADOURO, @NUMERO, @CEP, @BAIRRO, @CIDADE, @ESTADO);";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("LOGRADOURO", endereco.Logradouro);
            parameters.Add("NUMERO", endereco.Numero);
            parameters.Add("CEP", endereco.Cep);
            parameters.Add("BAIRRO", endereco.Bairro);
            parameters.Add("CIDADE", endereco.Cidade);
            parameters.Add("ESTADO", endereco.Estado);

            if (endereco.Id != 0) {
                parameters.Add("ID", endereco.Id);
                executeUpdateSql(sql, parameters);
            } else {
                endereco.Id = executeInsertSql(sql, parameters);
            }
        }

        private void salvarOuAtualizarPessoa(Pessoa pessoa) {
            bool atualizacao = pessoa.Id != 0;
            string sql = atualizacao ? 
                    "Update PESSOA Set NOME = @NOME, CPF = @CPF, ID_ENDERECO = @ID_ENDERECO Where ID = @ID" 
                    :
                    "Insert Into PESSOA(NOME, CPF, ID_ENDERECO) Values (@NOME, @CPF, @ID_ENDERECO);";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("NOME", pessoa.Nome);
            parameters.Add("CPF", pessoa.Cpf);
            parameters.Add("ID_ENDERECO", pessoa.Endereco?.Id);

            if (atualizacao) {
                parameters.Add("ID", pessoa.Id);
                executeUpdateSql(sql, parameters);
            } else {
                pessoa.Id = executeInsertSql(sql, parameters);
            }

            salvarOuAtualizarTelefonesPessoa(pessoa, atualizacao);
        }

        private void salvarOuAtualizarTelefone(Telefone telefone) {
            string sql = telefone.Id != 0 ? 
                    "Update TELEFONE Set NUMERO = @NUMERO, DDD = @DDD, TIPO = @TIPO Where ID = @ID" 
                    :
                    "Insert Into TELEFONE(NUMERO, DDD, TIPO) Values (@NUMERO, @DDD, @TIPO);";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("NUMERO", telefone.Numero);
            parameters.Add("DDD", telefone.Ddd);
            parameters.Add("TIPO", telefone.Tipo?.Id);

            if (telefone.Id != 0) {
                parameters.Add("ID", telefone.Id);
                executeUpdateSql(sql, parameters);
            } else {
                telefone.Id = executeInsertSql(sql, parameters);
            }
        }

        private void salvarOuAtualizarTelefonesPessoa(Pessoa p, bool atualizacao) {
            if (atualizacao) {
                deletarPessoaTelefone(p);
            }

            foreach(Telefone telefone in p.Telefones) {
                    salvarOuAtualizarTelefone(telefone);
                    executeInsertSql("Insert Into PESSOA_TELEFONE (ID_PESSOA, ID_TELEFONE) Values (@ID_PESSOA, @ID_TELEFONE)", 
                                     new Dictionary<string, object>{ {"@ID_PESSOA", p.Id}, {"@ID_PESSOA", telefone.Id}});
            }
        }

        private void deletarPessoaTelefone(Pessoa p) 
        {
            executeUpdateSql(@"Delete FROM PESSOA_TELEFONE Where ID_PESSOA = " + p.Id, null);
        }

        public Pessoa consulte(long cpf)
        {
            using (SqlConnection connection = getConnection())
            {
                IList<Dictionary<string, object>> dados = executeSql("Select * From PESSOA Where CPF = " + cpf);

                if (dados.Count > 0) {
                    Dictionary<string, object> linha = dados[0];

                    Pessoa pessoa = new Pessoa();
                    pessoa.Id = Convert.ToInt32(linha["ID"]);
                    pessoa.Nome = linha["NOME"].ToString();
                    pessoa.Cpf = Convert.ToInt64(linha["CPF"]);
                    pessoa.Endereco = buscarEnderecoPorId(Convert.ToInt32(linha["ID_ENDERECO"]));
                    pessoa.Telefones = buscarTelefonesPorPessoa(pessoa.Id);

                    return pessoa;

                } else {
                    Console.WriteLine("Pessoa não encontrada com o CPF " + cpf);
                }


                 
            }

            return null;
        }

        private Endereco buscarEnderecoPorId(int idEndereco) 
        {
            IList<Dictionary<string, object>> dados = executeSql("Select * From ENDERECO Where ID = " + idEndereco);

            if (dados.Count > 0) {
                Dictionary<string, object> linha = dados[0];
                Endereco endereco = new Endereco();

                endereco.Id = Convert.ToInt32(linha["ID"]);
                endereco.Logradouro = linha["LOGRADOURO"].ToString();
                endereco.Numero = Convert.ToInt32(linha["NUMERO"]);
                endereco.Cep = Convert.ToInt32(linha["CEP"]);
                endereco.Bairro = linha["BAIRRO"].ToString();
                endereco.Cidade = linha["CIDADE"].ToString();
                endereco.Estado = linha["ESTADO"].ToString();

                return endereco;
            } 
            return null;
        }

        private IList<Telefone> buscarTelefonesPorPessoa(int idPessoa) 
        {
            string sql = "Select tel.ID, tel.NUMERO, tel.DDD, tel.TIPO ID_TIPO, tp.TIPO NM_TIPO " +
                         "From TELEFONE tel Inner Join TELEFONE_TIPO ip On tel.TIPO = tp.ID " +
                         "   Inner Join PESSOA_TELEFONE pt On tel.ID = pt.ID_TELEFONE " +
                         "Where pt.ID_PESSOA = " + idPessoa;
            IList<Dictionary<string, object>> dados = executeSql(sql);

            IList<Telefone> telefones = new List<Telefone>();
            foreach (Dictionary<string, object> linha in dados) {
                Telefone telefone = new Telefone();
                telefone.Id = Convert.ToInt32(linha["ID"]);
                telefone.Numero = Convert.ToInt32(linha["NUMERO"]);;
                telefone.Ddd = Convert.ToInt32(linha["DDD"]);
                telefone.Tipo = new TipoTelefone(Convert.ToInt32(linha["ID_TIPO"]), linha["NM_TIPO"].ToString());
                
                telefones.Add(telefone);
            }
            return telefones;
        }
    }
}