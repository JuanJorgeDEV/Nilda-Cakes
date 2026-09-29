// Confirmação antes de ações rápidas (pagamento, entrada/perda de estoque...).
// Uso: data-confirmar="Registrar {forma}?" num <button> ou <a>. Nomes entre
// chaves são trocados pelo valor do campo de mesmo nome no formulário do botão
// (radio marcado, select ou input). data-confirmar-requer="quantidade" pula a
// pergunta enquanto esse campo estiver vazio (o servidor avisa o que falta).
document.addEventListener('click', function (evento) {
    var alvo = evento.target.closest('[data-confirmar]');
    if (!alvo) return;

    var form = alvo.form || alvo.closest('form');
    var campo = function (nome) {
        if (!form) return '';
        var el = form.elements[nome];
        if (!el) return '';
        if (el instanceof RadioNodeList) return el.value || '';
        return (el.value || '').trim();
    };

    var requer = alvo.dataset.confirmarRequer;
    if (requer && !campo(requer)) return;

    var mensagem = alvo.dataset.confirmar.replace(/\{(\w+)\}/g, function (_, nome) {
        return campo(nome);
    });

    if (!window.confirm(mensagem)) {
        evento.preventDefault();
        evento.stopPropagation();
    }
}, true);
