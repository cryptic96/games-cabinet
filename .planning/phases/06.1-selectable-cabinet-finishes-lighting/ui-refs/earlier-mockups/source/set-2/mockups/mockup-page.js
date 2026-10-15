/**
 * Mockup page wiring: reads finish, depth, boxes and sample from the query string (or the buttons), and redraws.
 * Query string example: index.html?finish=library&depth=deep&boxes=palette&sample=65&clean=1
 */
(function () {
  const DEFAULT_DEPTH = { current: 'flat', walnut: 'shaded', oak: 'shaded', library: 'deep', plywood: 'deep' };
  const params = new URLSearchParams(location.search);
  const state = {
    finish: params.get('finish') || 'walnut',
    depth: params.get('depth') || '',
    boxes: params.get('boxes') || 'palette',
    sample: params.get('sample') || '65',
  };

  const room = document.getElementById('room');
  const mount = document.getElementById('cabinet');
  const status = document.getElementById('status');
  const controls = document.getElementById('controls');

  if (params.get('clean') === '1') {
    controls.hidden = true;
  }

  /** Applies the state to the page and redraws the cabinet. */
  function draw() {
    const depth = state.depth || DEFAULT_DEPTH[state.finish] || 'shaded';
    room.dataset.finish = state.finish;
    room.dataset.depth = depth;
    const layout = window.CABINET_LAYOUTS[state.sample];
    const count = Number(state.sample);
    status.textContent = 'Invented collection of ' + count + (count === 1 ? ' item.' : ' items.');
    window.renderMockCabinet(mount, layout, { boxes: state.boxes });

    controls.querySelectorAll('.mock-group').forEach((group) => {
      const param = group.dataset.param;
      const current = param === 'depth' ? depth : state[param];
      group.querySelectorAll('button').forEach((button) => {
        button.setAttribute('aria-pressed', String(button.dataset.value === current));
      });
    });
  }

  controls.addEventListener('click', (event) => {
    const button = event.target.closest('button[data-value]');
    if (!button) {
      return;
    }
    const param = button.closest('.mock-group').dataset.param;
    state[param] = button.dataset.value;
    if (param === 'finish') {
      state.depth = '';
    }
    const query = new URLSearchParams(state);
    history.replaceState(null, '', '?' + query.toString());
    draw();
  });

  draw();
})();
