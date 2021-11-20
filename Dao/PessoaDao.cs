using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CadastroPessoas.Models.Dao
{
    public class PessoaDao
    {

         string connectionString = "Server=localhost\\SQLEXPRESS;Database=pim_viii;Trusted_Connection=True;Encrypt=False;"; 

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

             if (connection.State != ConnectionState.Open) 
                connection.Open();
             return connection;
         }

         private SqlCommand createSqlCommand(string sql, SqlTransaction transaction, Dictionary<string, object> parameters) 
         {
             var cmd = getConnection().CreateCommand();
             if (transaction != null)
                cmd.Transaction = transaction;
             cmd.CommandText = sql;
             
             if (parameters != null)
                foreach (string param in parameters.Keys)
                    cmd.Parameters.AddWithValue(param, parameters[param]);

            return cmd;
         }

         private int executeUpdateSql(string sql, SqlTransaction transaction, Dictionary<string, object> parameters)
         {
             using (var cmd = createSqlCommand(sql, transaction, parameters))
             {
                 return cmd.ExecuteNonQuery();
             };
         }

         private int executeInsertSql(string sql, SqlTransaction transaction, Dictionary<string, object> parameters)
         {
             sql += ";Select SCOPE_IDENTITY();";
             using (var cmd = createSqlCommand(sql, transaction, parameters))
             {
                 return Convert.ToInt32(cmd.ExecuteScalar());
             };
         }

        private IList<Dictionary<string, object>> executeSql(string sql) 
        {
            return executeSql(sql, null);
        }

         private IList<Dictionary<string, object>> executeSql(string sql, Dictionary<string, object> parameters)
         {
             using (var cmd = createSqlCommand(sql, null, parameters))
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
            using (var trans = getConnection().BeginTransaction()) 
            {
                 try
                {
                    deletarPessoaTelefone(p, trans);
                    executeUpdateSql(@"Delete FROM PESSOA Where ID = " + p.Id, trans, null);
                    
                    // Remover endereços órfãos
                    executeUpdateSql(@"Delete FROM ENDERECO Where not exists (Select 1 From PESSOA p Where p.ENDERECO = ENDERECO.ID)", trans, null);

                    // Remover telefone órfãos
                    executeUpdateSql(@"Delete FROM TELEFONE Where not exists (Select 1 From PESSOA_TELEFONE pt Where pt.ID_TELEFONE = TELEFONE.ID)", trans, null);

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
            using (var trans = getConnection().BeginTransaction()) 
            {
                try
                {
                    salvarOuAtualizarEndereco(p.Endereco, trans);
                    salvarOuAtualizarPessoa(p, trans);

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
            using (var trans = getConnection().BeginTransaction()) 
            {
                try
                {
                    salvarOuAtualizarEndereco(p.Endereco, trans);
                    salvarOuAtualizarPessoa(p, trans);

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

        private void salvarOuAtualizarEndereco(Endereco endereco, SqlTransaction trans) 
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

            if (endereco.Id != 0) 
            {
                parameters.Add("ID", endereco.Id);
                executeUpdateSql(sql, trans, parameters);
            } 
            else 
            {
                endereco.Id = executeInsertSql(sql, trans, parameters);
            }
        }

        private void salvarOuAtualizarPessoa(Pessoa pessoa, SqlTransaction trans) {
            bool atualizacao = pessoa.Id != 0;
            string sql = atualizacao ? 
                    "Update PESSOA Set NOME = @NOME, CPF = @CPF, ID_ENDERECO = @ENDERECO Where ID = @ID" 
                    :
                    "Insert Into PESSOA(NOME, CPF, ENDERECO) Values (@NOME, @CPF, @ENDERECO);";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("NOME", pessoa.Nome);
            parameters.Add("CPF", pessoa.Cpf);
            parameters.Add("ENDERECO", pessoa.Endereco?.Id);

            if (atualizacao) 
            {
                parameters.Add("ID", pessoa.Id);
                executeUpdateSql(sql, trans, parameters);
            } 
            else 
            {
                pessoa.Id = executeInsertSql(sql, trans, parameters);
            }

            salvarOuAtualizarTelefonesPessoa(pessoa, trans, atualizacao);
        }

        private void salvarOuAtualizarTelefone(Telefone telefone, SqlTransaction trans) {
            string sql = telefone.Id != 0 ? 
                    "Update TELEFONE Set NUMERO = @NUMERO, DDD = @DDD, TIPO = @TIPO Where ID = @ID" 
                    :
                    "Insert Into TELEFONE(NUMERO, DDD, TIPO) Values (@NUMERO, @DDD, @TIPO);";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("NUMERO", telefone.Numero);
            parameters.Add("DDD", telefone.Ddd);
            parameters.Add("TIPO", telefone.Tipo?.Id);

            if (telefone.Id != 0) 
            {
                parameters.Add("ID", telefone.Id);
                executeUpdateSql(sql, trans, parameters);
            } 
            else 
            {
                telefone.Id = executeInsertSql(sql, trans, parameters);
            }
        }

        private void salvarOuAtualizarTelefonesPessoa(Pessoa p, SqlTransaction trans, bool atualizacao) {
            if (atualizacao) 
                deletarPessoaTelefone(p, trans);

            if (p.Telefones != null)
                foreach(Telefone telefone in p.Telefones) 
                {
                        salvarOuAtualizarTelefone(telefone, trans);
                        executeUpdateSql("Insert Into PESSOA_TELEFONE (ID_PESSOA, ID_TELEFONE) Values (@ID_PESSOA, @ID_TELEFONE)", trans,
                                        new Dictionary<string, object>{ {"@ID_PESSOA", p.Id}, {"@ID_TELEFONE", telefone.Id}});
                }
        }

        private void deletarPessoaTelefone(Pessoa p, SqlTransaction trans) 
        {
            executeUpdateSql(@"Delete FROM PESSOA_TELEFONE Where ID_PESSOA = " + p.Id, trans, null);
        }

        public Pessoa consulte(long cpf)
        {
            using (SqlConnection connection = getConnection())
            {
                IList<Dictionary<string, object>> dados = executeSql("Select * From PESSOA Where CPF = " + cpf);

                if (dados.Count > 0) 
                {
                    Dictionary<string, object> linha = dados[0];

                    Pessoa pessoa = new Pessoa();
                    pessoa.Id = Convert.ToInt32(linha["ID"]);
                    pessoa.Nome = linha["NOME"].ToString();
                    pessoa.Cpf = Convert.ToInt64(linha["CPF"]);
                    pessoa.Endereco = buscarEnderecoPorId(Convert.ToInt32(linha["ENDERECO"]));
                    pessoa.Telefones = buscarTelefonesPorPessoa(pessoa.Id);

                    return pessoa;

                } 
                else 
                {
                    Console.WriteLine("Pessoa não encontrada com o CPF " + cpf);
                }


                 
            }

            return null;
        }

        private Endereco buscarEnderecoPorId(int idEndereco) 
        {
            IList<Dictionary<string, object>> dados = executeSql("Select * From ENDERECO Where ID = " + idEndereco);

            if (dados.Count > 0) 
            {
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
            string sql = "Select tel.ID, tel.NUMERO, tel.DDD, tel.TIPO ID_TIPO, tt.TIPO NM_TIPO " +
                         "From TELEFONE tel Inner Join TELEFONE_TIPO tt On tel.TIPO = tt.ID " +
                         "   Inner Join PESSOA_TELEFONE pt On tel.ID = pt.ID_TELEFONE " +
                         "Where pt.ID_PESSOA = " + idPessoa;
            IList<Dictionary<string, object>> dados = executeSql(sql);

            IList<Telefone> telefones = new List<Telefone>();
            foreach (Dictionary<string, object> linha in dados) 
            {
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