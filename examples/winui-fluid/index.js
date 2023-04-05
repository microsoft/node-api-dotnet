const { TinyliciousClient } = require("@fluidframework/tinylicious-client");
const { ConnectionState, SharedString } = require("fluid-framework");

/** @type import('@fluidframework/common-definitions').ITelemetryBaseLogger */
const logger = {
  send: (event) => {
    console.log(`[fluid:${event.category}] ${event.eventName}`);
  }
};

/** @returns {import('fluid-framework').SharedString} */
const getFluidData = async () => {
  const client = new TinyliciousClient({ logger });

  /** @type import('fluid-framework').ContainerSchema */
  const containerSchema = {
      initialObjects: { sharedString: SharedString }
  };
  /** @type import('fluid-framework').IFluidContainer */
  let container;
  const containerId = ''; // TODO: Get from UI? Or generate and show in UI?
  if (!containerId) {
      ({ container } = await client.createContainer(containerSchema));
      const id = await container.attach();
      console.log('container id: ' + id);
      //window.location.hash = id;
  } else {
    ({ container } = await client.getContainer(containerId, containerSchema));
    if (container.connectionState !== ConnectionState.Connected) {
        await new Promise((resolve) => {
            container.once("connected", resolve);
        });
        console.log('connected to ' + containerId);
    }
  }
  return container.initialObjects.sharedString;
};
getFluidData();
