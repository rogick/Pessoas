using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Pessoas.Models;

namespace Pessoas.Dao
{
    public class PessoaDAO: IDisposable
    {

         string connectionString = "Server=localhost\\SQLEXPRESS;Database=pim_viii;Trusted_Connection=True;Encrypt=False;"; 

         private SqlConnection connection;

        // Métodos relativos a conexão e execução de queries

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

         private void closeConnection() {
             if (connection != null && connection.State != ConnectionState.Closed) {
                connection.Close();
                connection.Dispose();
                connection = null;
             }
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
            return executeSql(sql, null, null);
        }

        private IList<Dictionary<string, object>> executeSql(string sql, SqlTransaction transaction) 
        {
            return executeSql(sql, transaction, null);
        }

         private IList<Dictionary<string, object>> executeSql(string sql, SqlTransaction transaction, Dictionary<string, object> parameters)
         {
            using (var cmd = createSqlCommand(sql, transaction, parameters))
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

        // Métodos públicos

        public bool exclua(Pessoa p)
        {
            using (var trans = getConnection().BeginTransaction()) 
            {
                 try
                {
                    validarExclusao(p);

                    excluirPessoaTelefone(p, trans);
                    executeUpdateSql(@"Delete FROM PESSOA Where ID = " + p.Id, trans, null);
                    
                    excluirRegistrosOrfaos(trans);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    throw new DAOException("Ocorreu um erro ao tentar excluir o registro", ex);
                }
            }
        }

        public bool insira(Pessoa p)
        {
            using (var trans = getConnection().BeginTransaction()) 
            {
                try
                {
                    validarSalvarAlterarPessoa(p, trans);

                    recuperarIdEnderecoSalvo(p.Endereco, trans);
                    salvarOuAtualizarEndereco(p.Endereco, trans);
                    salvarOuAtualizarPessoa(p, trans);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    throw new DAOException("Ocorreu um erro ao tentar inserir o registro", ex);
                }
            }
        }

        public bool altere(Pessoa p)
        {
            using (var trans = getConnection().BeginTransaction()) 
            {
                try
                {
                    validarSalvarAlterarPessoa(p, trans);

                    recuperarIdEnderecoSalvo(p.Endereco, trans);
                    salvarOuAtualizarEndereco(p.Endereco, trans);
                    salvarOuAtualizarPessoa(p, trans);
                    excluirRegistrosOrfaos(trans);

                    trans.Commit();
                    return true;
                     
                }
                catch (System.Exception ex)
                {
                    trans.Rollback();
                    throw new DAOException("Ocorreu um erro ao tentar alterar o registro", ex);
                    
                }
            }
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
                   throw new DAOException("Pessoa não encontrada com o CPF " + cpf);
                }
            }
        }


        // Métodos auxiliares para exclusão e alteração

        private void excluirPessoaTelefone(Pessoa p, SqlTransaction trans) 
        {
            executeUpdateSql(@"Delete FROM PESSOA_TELEFONE Where ID_PESSOA = " + p.Id, trans, null);
        }

        private void excluirRegistrosOrfaos(SqlTransaction trans) {
            executeUpdateSql(@"Delete FROM ENDERECO Where not exists (Select 1 From PESSOA p Where p.ENDERECO = ENDERECO.ID)", trans, null);
            executeUpdateSql(@"Delete FROM TELEFONE Where not exists (Select 1 From PESSOA_TELEFONE pt Where pt.ID_TELEFONE = TELEFONE.ID)", trans, null);
        }

        // Métodos auxiliares para inclusão e alteração do endereço

        private void recuperarIdEnderecoSalvo(Endereco endereco, SqlTransaction trans) {
            string sql = "Select ID From ENDERECO " +
                        "Where TRIM(UPPER(LOGRADOURO)) = @LOGRADOURO And NUMERO = @NUMERO And CEP = @CEP " +
                        " And TRIM(UPPER(BAIRRO)) = @BAIRRO AND TRIM(UPPER(CIDADE)) = @CIDADE " +
                        " AND TRIM(UPPER(ESTADO)) = @ESTADO";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("LOGRADOURO", endereco.Logradouro);
            parameters.Add("NUMERO", endereco.Numero);
            parameters.Add("CEP", endereco.Cep);
            parameters.Add("BAIRRO", endereco.Bairro);
            parameters.Add("CIDADE", endereco.Cidade);
            parameters.Add("ESTADO", endereco.Estado);

            IList<Dictionary<string, object>> list = executeSql(sql, trans, parameters);
            if (list.Count > 0 && list[0].ContainsKey("ID") && list[0]["ID"] != null)
                endereco.Id = Convert.ToInt32(list[0]["ID"]);
            else
                endereco.Id = 0;

        }

        private void salvarOuAtualizarEndereco(Endereco endereco, SqlTransaction trans) 
        {
            string sql = endereco.Id != 0 ? 
                    "Update ENDERECO Set LOGRADOURO = @LOGRADOURO, NUMERO = @NUMERO, CEP = @CEP, BAIRRO = @BAIRRO,  " +
                    "   CIDADE = @CIDADE, ESTADO = @ESTADO " +
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
                    "Update PESSOA Set NOME = @NOME, CPF = @CPF, ENDERECO = @ENDERECO Where ID = @ID" 
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

        // Métodos auxiliares para inclusão e alteração dos telefones

        private void salvarOuAtualizarTelefonesPessoa(Pessoa p, SqlTransaction trans, bool atualizacao) {
            if (atualizacao) 
                excluirPessoaTelefone(p, trans);

            if (p.Telefones != null) 
            {
                IList<int> idTelefones = new List<int>();
                foreach(Telefone telefone in p.Telefones) 
                {                    
                    recuperarIdTelefoneSalvo(telefone, trans);
                    salvarOuAtualizarTelefone(telefone, trans);
                    executeUpdateSql("Insert Into PESSOA_TELEFONE (ID_PESSOA, ID_TELEFONE) Values (@ID_PESSOA, @ID_TELEFONE)", trans,
                                    new Dictionary<string, object>{ {"@ID_PESSOA", p.Id}, {"@ID_TELEFONE", telefone.Id}});
                }
            }
        }

        private void recuperarIdTelefoneSalvo(Telefone telefone, SqlTransaction trans) {
            string sql = "Select ID From TELEFONE " +
                        "Where NUMERO = @NUMERO And DDD = @DDD And TIPO = @TIPO ";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("NUMERO", telefone.Numero);
            parameters.Add("DDD", telefone.Ddd);
            parameters.Add("TIPO", telefone.Tipo?.Id);

            IList<Dictionary<string, object>> list = executeSql(sql, trans, parameters);
            if (list.Count > 0 && list[0].ContainsKey("ID") && list[0]["ID"] != null)
                telefone.Id = Convert.ToInt32(list[0]["ID"]);
            else
                telefone.Id = 0;
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

        // Métodos de validação

        private void validarSalvarAlterarPessoa(Pessoa p, SqlTransaction transaction) {
            ValidacaoException validacao = new ValidacaoException();

            if (String.IsNullOrEmpty(p?.Nome))
                validacao.addMessage("Campo Nome é obrigatório");
            else if (p.Nome.Length > 256)
                validacao.addMessage("Tamanho máximo para campo Nome é de 256 caracteres");

            if (p?.Cpf <= 0)
                validacao.addMessage("Campo CPF é obrigatório");
            else 
            {
                IList<Dictionary<string, object>> rs = executeSql("Select ID From PESSOA Where CPF = " + p.Cpf, transaction, null);
                foreach (var linha in rs)
                {
                    if (linha != null && linha["ID"] != null && Convert.ToInt32(linha["ID"]) != p.Id) {
                        validacao.addMessage("Já existe outra pessoa com o mesmo CPF cadastrada no sistema");
                        break;
                    }
                }
            }

            if (p.Endereco == null)
                validacao.addMessage("Endereço é obrigatório");
            else
                validarSalvarAlterarEndereco(p.Endereco, validacao);

            if (p.Telefones == null || p.Telefones.Count == 0)
                validacao.addMessage("É obrigatório a inclusão de pelo menos um telefone");
            else 
            {
                IList<string> numeros = new List<string>();
                foreach (var tel in p.Telefones)
                {
                    string hash = String.Format("{0}-{1}-{2}", tel?.Ddd, tel?.Numero, tel?.Tipo?.Id);
                    int posicao = p.Telefones.IndexOf(tel) + 1;
                    if (!numeros.Contains(hash))
                    {
                        validarSalvarAlterarTelefone(tel, posicao, validacao, transaction);
                        numeros.Add(hash);
                    } else
                        validacao.addMessage(String.Format("Telefone {0}: Já informado para esta pessoa", posicao));
                }
            }

            validacao.NotifyException();
        }

        private void validarSalvarAlterarEndereco(Endereco endereco, ValidacaoException validacao) {
            if (String.IsNullOrEmpty(endereco?.Logradouro))
                validacao.addMessage("Campo Logradouro é obrigatório");
            else if (endereco.Logradouro.Length > 256)
                validacao.addMessage("Tamanho máximo para campo Logradouro é de 256 caracteres");

            if (endereco?.Cep <= 0)
                validacao.addMessage("Campo CEP é obrigatório");

            if (String.IsNullOrEmpty(endereco?.Bairro))
                validacao.addMessage("Campo Bairro é obrigatório");
            else if (endereco.Logradouro.Length > 50)
                validacao.addMessage("Tamanho máximo para campo Bairro é de 50 caracteres");

            if (String.IsNullOrEmpty(endereco?.Cidade))
                validacao.addMessage("Campo Cidade é obrigatório");
            else if (endereco.Cidade.Length > 30)
                validacao.addMessage("Tamanho máximo para campo Cidade é de 30 caracteres");

            if (String.IsNullOrEmpty(endereco?.Estado))
                validacao.addMessage("Campo Estado é obrigatório");
            else if (endereco.Estado.Length > 20)
                validacao.addMessage("Tamanho máximo para campo Estado é de 20 caracteres");
        }

        private void validarSalvarAlterarTelefone(Telefone telefone, int posicao, ValidacaoException validacao, SqlTransaction transaction) {
            if (telefone?.Numero <= 0)
                validacao.addMessage(String.Format("Telefone {0}: Campo Número do Telefone é obrigatório", posicao));

            if (telefone?.Ddd <= 0)
                validacao.addMessage(String.Format("Telefone {0}: Campo DDD é obrigatório", posicao));

            if (telefone?.Tipo == null || telefone.Tipo.Id <= 0)
                validacao.addMessage(String.Format("Telefone {0}: Campo Tipo é obrigatório", posicao));
            else {
                IList<Dictionary<string, object>> list = executeSql("Select 1 From TELEFONE_TIPO Where ID = " + telefone.Tipo.Id, transaction, null);
                if (list.Count == 0)
                    validacao.addMessage(String.Format("Telefone {0}: Valor inválido para o campo Tipo: " + telefone.Tipo.Id, posicao));
            }
        }
        
        private void validarExclusao(Pessoa p) {
            ValidacaoException validacao = new ValidacaoException();

            if (p?.Id == null)
                validacao.addMessage("ID da Pessoa não informado");

            if (p?.Endereco?.Id == null)
                validacao.addMessage("ID do Endereço não informado");

            validacao.NotifyException();
        }


        // Métodos auxiliares para carregar os dados das entidades relacionadas à Pessoa

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

        public void Dispose() {
            closeConnection();
        }
    }

    public class DAOException: Exception {

        public DAOException(string message): base(message)
        {
        }

        public DAOException(string message, Exception innerException): base(message, innerException)
        {
        }
        
    }

    public class ValidacaoException: Exception {

        private IList<string> messages;

        public IList<string> getMessages() {
            if (this.messages == null) this.messages = new List<string>();

            return this.messages;
        }

        public void addMessage(string msg) {
            getMessages().Add(msg);
        }

        public void NotifyException() {
            if (this.getMessages().Count > 0)
                throw this;
        }

        public override string Message 
        { 
            get
            {
                string retorno = "Não foi possível efetuar as operações devido às seguintes falhas nas regras de validação\n";
                foreach (string msg in this.getMessages()) 
                    retorno += "\n\t" + msg;
                
                return retorno;
            }
        }

    }
}